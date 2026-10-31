using System.Globalization;
using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels.Ai;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Application.Services;

public class AiService(
    IAppDbContext db,
    IBackgroundTaskQueue queue,
    IGeminiClient gemini,
    IFileStorageService storage,
    IQuotationPdfRenderer pdfRenderer,
    IAiUsageService aiUsage,
    IFeatureAccessService featureAccess) : IAiService
{
    // Same fail-closed contract as IFeatureAccessService itself: no subscription or the
    // plan's ai_features row disabled/absent both come back false here, matching
    // FeatureAccessService.HasFeatureAsync's SingleOrDefaultAsync (missing row -> Enabled
    // defaults to false, not "unlimited"). Mirrors ILimitEnforcementService.CheckLimitAsync's
    // call-at-the-top-of-every-mutating-entry-point placement.
    private async Task EnsureAiFeatureEnabledAsync(string companyId, CancellationToken cancellationToken)
    {
        if (!await featureAccess.HasFeatureAsync(companyId, FeatureKeys.AiFeatures, cancellationToken))
        {
            throw new ForbiddenException("AI features are not available on your current plan");
        }
    }

    public async Task<QueuedResponse> SummarizeAsync(string companyId, string userId, SummarizeRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAiFeatureEnabledAsync(companyId, cancellationToken);

        var exists = await db.Communications.AnyAsync(c => c.Id == request.CommunicationId, cancellationToken);
        if (!exists) throw new NotFoundException("Communication not found");

        await aiUsage.RecordAsync(companyId, userId, AiUsageEndpoints.Summarize, cancellationToken: cancellationToken);
        await queue.QueueAsync(async (sp, ct) => await sp.GetRequiredService<IAiService>().ProcessSummarizeAsync(request.CommunicationId, ct));
        return new QueuedResponse(true, request.CommunicationId);
    }

    public async Task<AiSummaryDto> GetSummaryAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var summary = await db.AiSummaries.SingleOrDefaultAsync(s => s.Id == id || s.CommunicationId == id, cancellationToken)
            ?? throw new NotFoundException("AI summary not found");

        return new AiSummaryDto(summary.Id, summary.CommunicationId, summary.Summary, summary.Sentiment?.ToString(), summary.ActionItemsJson, summary.NextFollowUp, summary.GeneratedAt);
    }

    public async Task<QueuedResponse> QueryAsync(string companyId, string userId, AiQueryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAiFeatureEnabledAsync(companyId, cancellationToken);

        var result = new AiQueryResult { CompanyId = companyId, Question = request.Question };
        db.AiQueryResults.Add(result);
        await db.SaveChangesAsync(cancellationToken);

        await aiUsage.RecordAsync(companyId, userId, AiUsageEndpoints.Query, cancellationToken: cancellationToken);
        await queue.QueueAsync(async (sp, ct) => await sp.GetRequiredService<IAiService>().ProcessQueryAsync(result.Id, ct));
        return new QueuedResponse(true, result.Id);
    }

    public async Task<AiQueryResultDto> GetQueryResultAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var result = await db.AiQueryResults.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Query result not found");

        if (result.Status == AiJobStatus.Failed)
        {
            throw new ValidationAppException([result.ErrorMessage ?? "The AI could not answer this question"]);
        }

        return ToQueryDto(result);
    }

    public async Task<QueuedResponse> DraftEmailAsync(string companyId, string userId, AiDraftEmailRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAiFeatureEnabledAsync(companyId, cancellationToken);

        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        var draft = new AiEmailDraft { CompanyId = companyId, ContactId = contact.Id, Context = request.Context };
        db.AiEmailDrafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);

        await aiUsage.RecordAsync(companyId, userId, AiUsageEndpoints.DraftEmail, cancellationToken: cancellationToken);
        await queue.QueueAsync(async (sp, ct) => await sp.GetRequiredService<IAiService>().ProcessDraftEmailAsync(draft.Id, ct));
        return new QueuedResponse(true, draft.Id);
    }

    public async Task<AiEmailDraftDto> GetDraftEmailAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var draft = await db.AiEmailDrafts.SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Draft not found");

        if (draft.Status == AiJobStatus.Failed)
        {
            throw new ValidationAppException([draft.ErrorMessage ?? "The AI could not draft this email"]);
        }

        return new AiEmailDraftDto(draft.Id, draft.ContactId, draft.Status.ToString(), draft.Subject, draft.Body, draft.ErrorMessage);
    }

    public async Task<QueuedResponse> GenerateQuotationAsync(string companyId, string userId, AiGenerateQuotationRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAiFeatureEnabledAsync(companyId, cancellationToken);

        var deal = await db.Deals.SingleOrDefaultAsync(d => d.Id == request.DealId, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        var itemsJson = JsonSerializer.Serialize(request.Items);
        var quotation = new AiQuotation { CompanyId = companyId, DealId = deal.Id, RequestedByUserId = userId, ItemsJson = itemsJson };
        db.AiQuotations.Add(quotation);
        await db.SaveChangesAsync(cancellationToken);

        await aiUsage.RecordAsync(companyId, userId, AiUsageEndpoints.GenerateQuotation, cancellationToken: cancellationToken);
        await queue.QueueAsync(async (sp, ct) => await sp.GetRequiredService<IAiService>().ProcessQuotationAsync(quotation.Id, ct));
        return new QueuedResponse(true, quotation.Id);
    }

    public async Task<AiQuotationDto> GetQuotationAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var quotation = await db.AiQuotations.Include(q => q.FileAsset).SingleOrDefaultAsync(q => q.Id == id, cancellationToken)
            ?? throw new NotFoundException("Quotation not found");

        if (quotation.Status == AiJobStatus.Failed)
        {
            throw new ValidationAppException([quotation.ErrorMessage ?? "Quotation generation failed"]);
        }

        var downloadUrl = quotation.FileAsset is null ? null : storage.GetPresignedDownloadUrl(quotation.FileAsset.S3Key);
        return new AiQuotationDto(quotation.Id, quotation.DealId, quotation.ItemsJson, quotation.TotalAmount, quotation.Status.ToString(), downloadUrl, quotation.ErrorMessage);
    }

    public async Task ProcessSummarizeAsync(string communicationId, CancellationToken cancellationToken = default)
    {
        var communication = await db.Communications.Include(c => c.Contact).SingleOrDefaultAsync(c => c.Id == communicationId, cancellationToken);
        if (communication is null) return;

        var thread = await db.Communications
            .Where(c => c.ContactId == communication.ContactId)
            .OrderBy(c => c.Timestamp)
            .Select(c => $"[{c.Direction}] {c.Content}")
            .ToListAsync(cancellationToken);

        var prompt = $"""
            Summarize this customer communication thread. Reply as JSON with keys:
            summary (string), sentiment ("POSITIVE"|"NEUTRAL"|"NEGATIVE"), action_items (array of strings), next_follow_up (ISO date or null).

            Thread:
            {string.Join('\n', thread)}
            """;

        try
        {
            var raw = await gemini.GenerateAsync(prompt, jsonMode: true, maxOutputTokens: 1024, cancellationToken: cancellationToken);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var summary = await db.AiSummaries.SingleOrDefaultAsync(s => s.CommunicationId == communicationId, cancellationToken);
            if (summary is null)
            {
                summary = new AiSummary { CommunicationId = communicationId };
                db.AiSummaries.Add(summary);
            }

            summary.Summary = root.GetProperty("summary").GetString() ?? string.Empty;
            summary.Sentiment = root.TryGetProperty("sentiment", out var s) && Enum.TryParse<Sentiment>(s.GetString(), true, out var sentiment) ? sentiment : null;
            summary.ActionItemsJson = root.TryGetProperty("action_items", out var items) ? items.GetRawText() : null;
            summary.NextFollowUp = root.TryGetProperty("next_follow_up", out var nf) && nf.ValueKind == JsonValueKind.String && DateTime.TryParse(nf.GetString(), out var date) ? date : null;
            summary.GeneratedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // No persistent failure state on AiSummary (upsert-only design, matching the original) - leave it absent.
        }
    }

    public async Task ProcessQueryAsync(string aiQueryResultId, CancellationToken cancellationToken = default)
    {
        var result = await db.AiQueryResults.SingleOrDefaultAsync(r => r.Id == aiQueryResultId, cancellationToken);
        if (result is null) return;

        try
        {
            var classifyPrompt = $$"""
                Classify this question into one intent: STALE_CONTACTS, TOP_SALESPERSON, REVENUE_BY_PERIOD, or UNKNOWN.
                Reply as JSON: {"intent": "...", "days": number|null, "months": number|null}.
                Question: {{result.Question}}
                """;
            var classifyRaw = await gemini.GenerateAsync(classifyPrompt, jsonMode: true, maxOutputTokens: 256, cancellationToken: cancellationToken);
            using var classifyDoc = JsonDocument.Parse(classifyRaw);
            var intentStr = classifyDoc.RootElement.GetProperty("intent").GetString();

            if (!Enum.TryParse<QueryIntent>(ToPascalCase(intentStr), out var intent))
            {
                result.Status = AiJobStatus.Failed;
                result.ErrorMessage = "Could not understand the question";
                result.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var days = classifyDoc.RootElement.TryGetProperty("days", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : 14;
            var months = classifyDoc.RootElement.TryGetProperty("months", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : 6;

            object rows = intent switch
            {
                QueryIntent.StaleContacts => await RunStaleContactsAsync(result.CompanyId, days, cancellationToken),
                QueryIntent.TopSalesperson => await RunTopSalespersonAsync(result.CompanyId, cancellationToken),
                QueryIntent.RevenueByPeriod => await RunRevenueByPeriodAsync(result.CompanyId, months, cancellationToken),
                _ => Array.Empty<object>(),
            };

            var resultType = intent switch
            {
                QueryIntent.StaleContacts => AiResultType.ClientList,
                QueryIntent.TopSalesperson => AiResultType.SalesRanking,
                _ => AiResultType.Text,
            };

            var rowsJson = JsonSerializer.Serialize(rows);
            var answerPrompt = $"In one sentence, answer the question '{result.Question}' using this data: {rowsJson}";
            var answer = await gemini.GenerateAsync(answerPrompt, maxOutputTokens: 256, cancellationToken: cancellationToken);

            result.Intent = intent;
            result.ResultType = resultType;
            result.ResultRowsJson = rowsJson;
            result.Answer = answer.Trim();
            result.Status = AiJobStatus.Completed;
            result.CompletedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        catch (Exception ex)
        {
            result.Status = AiJobStatus.Failed;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ProcessDraftEmailAsync(string aiEmailDraftId, CancellationToken cancellationToken = default)
    {
        var draft = await db.AiEmailDrafts.Include(d => d.Contact).SingleOrDefaultAsync(d => d.Id == aiEmailDraftId, cancellationToken);
        if (draft is null) return;

        try
        {
            var thread = await db.Communications
                .Where(c => c.ContactId == draft.ContactId)
                .OrderByDescending(c => c.Timestamp)
                .Take(20)
                .Select(c => $"[{c.Direction}] {c.Content}")
                .ToListAsync(cancellationToken);

            var prompt = $$"""
                Draft a follow-up email to {{draft.Contact.FirstName}} {{draft.Contact.LastName}}.
                Extra context: {{draft.Context}}
                Recent communication history:
                {{string.Join('\n', thread)}}

                Reply as JSON: {"subject": "...", "body": "..."}
                """;

            var raw = await gemini.GenerateAsync(prompt, jsonMode: true, maxOutputTokens: 1024, cancellationToken: cancellationToken);
            using var doc = JsonDocument.Parse(raw);

            draft.Subject = doc.RootElement.GetProperty("subject").GetString();
            draft.Body = doc.RootElement.GetProperty("body").GetString();
            draft.Status = AiJobStatus.Completed;
            draft.CompletedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        catch (Exception ex)
        {
            draft.Status = AiJobStatus.Failed;
            draft.ErrorMessage = ex.Message;
            draft.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ProcessQuotationAsync(string aiQuotationId, CancellationToken cancellationToken = default)
    {
        var quotation = await db.AiQuotations.Include(q => q.Deal).SingleOrDefaultAsync(q => q.Id == aiQuotationId, cancellationToken);
        if (quotation is null) return;

        FileAsset? fileAsset = null;
        try
        {
            var items = JsonSerializer.Deserialize<List<QuotationItemRequest>>(quotation.ItemsJson) ?? [];
            var total = items.Sum(i => i.Quantity * i.UnitPrice);
            var pdfItems = items.Select(i => new QuotationLineItem(i.Description, i.Quantity, i.UnitPrice)).ToList();
            var pdfBytes = pdfRenderer.Render(quotation.Deal.Title, pdfItems, total, quotation.Deal.Currency);

            var key = storage.BuildKey(quotation.CompanyId, $"quotation-{quotation.Deal.Title}.pdf");
            await storage.UploadAsync(key, pdfBytes, "application/pdf", cancellationToken);

            fileAsset = new FileAsset
            {
                CompanyId = quotation.CompanyId,
                DealId = quotation.DealId,
                UploadedById = quotation.RequestedByUserId ?? throw new InvalidOperationException("Quotation has no requesting user recorded"),
                S3Key = key,
                Filename = $"quotation-{quotation.Deal.Title}.pdf",
                MimeType = "application/pdf",
                SizeBytes = pdfBytes.Length,
            };
            db.FileAssets.Add(fileAsset);

            quotation.TotalAmount = total;
            quotation.FileAssetId = fileAsset.Id;
            quotation.Status = AiJobStatus.Completed;
            quotation.CompletedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        catch (Exception ex)
        {
            if (fileAsset is not null) db.FileAssets.Remove(fileAsset);
            quotation.FileAssetId = null;
            quotation.TotalAmount = null;
            quotation.Status = AiJobStatus.Failed;
            quotation.ErrorMessage = ex.Message;
            quotation.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AiClientListResultDto> RunStaleContactsAsync(string companyId, int days, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var contacts = await db.Contacts
            .Where(c => c.CompanyId == companyId && !db.Communications.Any(cm => cm.ContactId == c.Id && cm.Timestamp >= cutoff))
            .Select(c => new
            {
                c.Id,
                Name = $"{c.FirstName} {c.LastName}",
                LastContactDate = db.Communications
                    .Where(cm => cm.ContactId == c.Id)
                    .OrderByDescending(cm => cm.Timestamp)
                    .Select(cm => (DateTime?)cm.Timestamp)
                    .FirstOrDefault(),
            })
            .Take(50)
            .ToListAsync(cancellationToken);

        var contactIds = contacts.Select(c => c.Id).ToList();
        var openDeals = await db.Deals
            .Where(d => contactIds.Contains(d.ContactId) && d.Stage != DealStage.Won && d.Stage != DealStage.Lost)
            .Select(d => new { d.Value, d.Currency })
            .ToListAsync(cancellationToken);

        return new AiClientListResultDto(
            contacts.Count,
            openDeals.Sum(d => d.Value),
            openDeals.Select(d => d.Currency).FirstOrDefault() ?? "TJS",
            contacts.Select(c => new AiClientContactDto(c.Id, c.Name.Trim(), c.LastContactDate)).ToList());
    }

    private async Task<AiSalesRankingResultDto> RunTopSalespersonAsync(string companyId, CancellationToken cancellationToken)
    {
        var results = await db.Deals
            .Where(d => d.CompanyId == companyId && d.Stage == DealStage.Won && d.OwnerId != null)
            .GroupBy(d => d.OwnerId)
            .Select(g => new { OwnerId = g.Key!, TotalValue = g.Sum(d => d.Value), Count = g.Count() })
            .OrderByDescending(g => g.TotalValue)
            .Take(5)
            .ToListAsync(cancellationToken);

        var ownerIds = results.Select(r => r.OwnerId).ToList();
        var users = await db.Users.Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        return new AiSalesRankingResultDto(
            results.Select(r => new AiSalesRankingEntryDto(r.OwnerId, users.GetValueOrDefault(r.OwnerId, "Unknown"), r.Count, r.TotalValue)).ToList());
    }

    private async Task<object> RunRevenueByPeriodAsync(string companyId, int months, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-months);
        var deals = await db.Deals
            .Where(d => d.CompanyId == companyId && d.Stage == DealStage.Won && d.UpdatedAt >= cutoff)
            .ToListAsync(cancellationToken);

        return deals
            .GroupBy(d => new { d.UpdatedAt.Year, d.UpdatedAt.Month })
            .Select(g => new { Period = $"{g.Key.Year}-{g.Key.Month:D2}", Revenue = g.Sum(d => d.Value) })
            .OrderBy(g => g.Period)
            .ToList();
    }

    private static string ToPascalCase(string? screamingSnakeCase)
    {
        if (string.IsNullOrEmpty(screamingSnakeCase)) return string.Empty;
        return string.Concat(screamingSnakeCase.Split('_').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }

    private static AiQueryResultDto ToQueryDto(AiQueryResult r) =>
        new(r.Id, r.Question, r.Intent?.ToString(), r.ResultType.ToString(), r.Status.ToString(), r.ResultRowsJson, r.Answer, r.ErrorMessage);

    // Cash-flow prediction for a single calendar month, computed synchronously from
    // Deals/Invoices (no LLM call, no job/poll needed - same rationale as
    // RevenueReportService's open-pipeline forecast, just bucketed to a specific period
    // instead of "right now").
    public async Task<AiForecastDto> GetForecastAsync(string companyId, string userId, AiForecastRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAiFeatureEnabledAsync(companyId, cancellationToken);

        var (periodStart, periodEnd, periodLabel) = ResolveForecastPeriod(request.Period);

        var dealContributions = await db.Deals
            .Where(d => d.Stage != DealStage.Won && d.Stage != DealStage.Lost
                && d.ExpectedCloseDate != null && d.ExpectedCloseDate >= periodStart && d.ExpectedCloseDate < periodEnd)
            .Select(d => new { d.ContactId, ContactName = d.Contact.FirstName + " " + d.Contact.LastName, Amount = d.Value * d.Probability / 100m, d.Currency, d.Probability })
            .ToListAsync(cancellationToken);

        var invoiceContributions = await db.Invoices
            .Where(i => (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.Overdue) && i.DueDate >= periodStart && i.DueDate < periodEnd)
            .Select(i => new { i.ContactId, ContactName = i.Contact.FirstName + " " + i.Contact.LastName, Amount = i.Amount, i.Currency })
            .ToListAsync(cancellationToken);

        var contributions = dealContributions
            .Select(c => (c.ContactId, ContactName: c.ContactName.Trim(), c.Amount, c.Currency))
            .Concat(invoiceContributions.Select(c => (c.ContactId, ContactName: c.ContactName.Trim(), c.Amount, c.Currency)))
            .ToList();

        var estimatedAmount = contributions.Sum(c => c.Amount);
        var currency = contributions
            .GroupBy(c => c.Currency)
            .OrderByDescending(g => g.Sum(c => c.Amount))
            .Select(g => g.Key)
            .FirstOrDefault() ?? "TJS";

        var confidencePercent = dealContributions.Count == 0
            ? 0d
            : Math.Round(dealContributions.Average(d => (double)d.Probability), 1);

        var topPayments = contributions
            .GroupBy(c => new { c.ContactId, c.ContactName })
            .Select(g => new TopExpectedPaymentDto(g.Key.ContactId, g.Key.ContactName, g.Sum(c => c.Amount)))
            .OrderByDescending(p => p.Amount)
            .Take(5)
            .ToList();

        await aiUsage.RecordAsync(companyId, userId, AiUsageEndpoints.Forecast, cancellationToken: cancellationToken);

        return new AiForecastDto(estimatedAmount, currency, confidencePercent, periodLabel, topPayments);
    }

    private static (DateTime Start, DateTime End, string Label) ResolveForecastPeriod(string? period)
    {
        DateTime start;
        if (period is not null && DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            start = DateTime.SpecifyKind(new DateTime(parsed.Year, parsed.Month, 1), DateTimeKind.Utc);
        }
        else
        {
            var next = DateTime.UtcNow.AddMonths(1);
            start = new DateTime(next.Year, next.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        var end = start.AddMonths(1);
        return (start, end, start.ToString("yyyy-MM", CultureInfo.InvariantCulture));
    }

    public async Task<AiAssistantSendResultDto> SendAssistantMessageAsync(string companyId, string userId, AiAssistantMessageRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureAiFeatureEnabledAsync(companyId, cancellationToken);

        AiAssistantConversation conversation;
        if (request.ConversationId is not null)
        {
            conversation = await db.AiAssistantConversations.SingleOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken)
                ?? throw new NotFoundException("Conversation not found");
        }
        else
        {
            conversation = new AiAssistantConversation
            {
                CompanyId = companyId,
                UserId = userId,
                Title = request.Message.Length > 60 ? request.Message[..60] : request.Message,
            };
            db.AiAssistantConversations.Add(conversation);
        }

        conversation.UpdatedAt = DateTime.UtcNow;

        var userMessage = new AiAssistantMessage
        {
            CompanyId = companyId,
            ConversationId = conversation.Id,
            Role = AiAssistantRole.User,
            Content = request.Message,
            Status = AiJobStatus.Completed,
        };
        db.AiAssistantMessages.Add(userMessage);

        var assistantMessage = new AiAssistantMessage
        {
            CompanyId = companyId,
            ConversationId = conversation.Id,
            Role = AiAssistantRole.Assistant,
            Content = string.Empty,
            Status = AiJobStatus.Pending,
        };
        db.AiAssistantMessages.Add(assistantMessage);

        await db.SaveChangesAsync(cancellationToken);

        await aiUsage.RecordAsync(companyId, userId, AiUsageEndpoints.Assistant, cancellationToken: cancellationToken);
        await queue.QueueAsync(async (sp, ct) => await sp.GetRequiredService<IAiService>().ProcessAssistantMessageAsync(assistantMessage.Id, ct));

        return new AiAssistantSendResultDto(conversation.Id, userMessage.Id, assistantMessage.Id);
    }

    public async Task<AiAssistantConversationDto> GetAssistantConversationAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var conversation = await db.AiAssistantConversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Conversation not found");

        return new AiAssistantConversationDto(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            conversation.Messages.Select(m => new AiAssistantMessageDto(m.Id, m.Role.ToString(), m.Content, m.Status.ToString(), m.CreatedAt)).ToList());
    }

    public async Task ProcessAssistantMessageAsync(string assistantMessageId, CancellationToken cancellationToken = default)
    {
        var message = await db.AiAssistantMessages.SingleOrDefaultAsync(m => m.Id == assistantMessageId, cancellationToken);
        if (message is null) return;

        try
        {
            var history = await db.AiAssistantMessages
                .Where(m => m.ConversationId == message.ConversationId && m.Id != message.Id)
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
                .Select(m => $"[{m.Role}] {m.Content}")
                .ToListAsync(cancellationToken);
            history.Reverse();

            var latestQuestion = await db.AiAssistantMessages
                .Where(m => m.ConversationId == message.ConversationId && m.Role == AiAssistantRole.User)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Content)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var classifyPrompt = $$"""
                A user asked a CRM assistant a question. Decide what company data (if any) is needed to answer it accurately.
                Reply as JSON: {"dataIntent": "DEALS"|"CONTACTS"|"TASKS"|"NONE", "stage": "Lead"|"Qualified"|"Proposal"|"Negotiation"|"Won"|"Lost"|null}.
                Only set "stage" when dataIntent is "DEALS" and the question names a specific pipeline stage.
                Question: {{latestQuestion}}
                """;
            var classifyRaw = await gemini.GenerateAsync(classifyPrompt, jsonMode: true, maxOutputTokens: 128, cancellationToken: cancellationToken);
            using var classifyDoc = JsonDocument.Parse(classifyRaw);
            var dataIntent = classifyDoc.RootElement.TryGetProperty("dataIntent", out var di) ? di.GetString() : null;
            DealStage? stageFilter = classifyDoc.RootElement.TryGetProperty("stage", out var st)
                && st.ValueKind == JsonValueKind.String
                && Enum.TryParse<DealStage>(st.GetString(), true, out var parsedStage)
                ? parsedStage
                : null;

            string? dataContextJson = dataIntent switch
            {
                "DEALS" => JsonSerializer.Serialize(await db.Deals
                    .Where(d => d.CompanyId == message.CompanyId && !d.IsArchived && (stageFilter == null || d.Stage == stageFilter))
                    .OrderByDescending(d => d.UpdatedAt)
                    .Take(30)
                    .Select(d => new { d.Title, Stage = d.Stage.ToString(), d.Value, d.Currency, d.Probability, d.ExpectedCloseDate, ContactName = d.Contact.FirstName + " " + d.Contact.LastName })
                    .ToListAsync(cancellationToken)),
                "CONTACTS" => JsonSerializer.Serialize(await db.Contacts
                    .Where(c => c.CompanyId == message.CompanyId && !c.IsArchived)
                    .OrderByDescending(c => c.UpdatedAt)
                    .Take(30)
                    .Select(c => new { Name = c.FirstName + " " + c.LastName, c.Email, c.Phone, c.CompanyName })
                    .ToListAsync(cancellationToken)),
                "TASKS" => JsonSerializer.Serialize(await db.Tasks
                    .Where(t => t.CompanyId == message.CompanyId && !t.IsArchived && t.Status != TaskItemStatus.Completed)
                    .OrderBy(t => t.DueDate)
                    .Take(30)
                    .Select(t => new { t.Title, Status = t.Status.ToString(), Priority = t.Priority.ToString(), t.DueDate })
                    .ToListAsync(cancellationToken)),
                _ => null,
            };

            var prompt = $$"""
                You are a helpful business assistant embedded in a CRM. Continue this conversation naturally and concisely.
                Only answer questions about the company's deals, contacts, or tasks using the data provided below - never invent names, values, or dates. If the data below doesn't contain what's needed, say so plainly instead of guessing.
                {{(dataContextJson is null ? "" : $"\nRelevant company data (JSON): {dataContextJson}\n")}}
                {{string.Join('\n', history)}}
                [Assistant]
                """;

            var reply = await gemini.GenerateAsync(prompt, maxOutputTokens: 1024, cancellationToken: cancellationToken);

            message.Content = reply.Trim();
            message.Status = AiJobStatus.Completed;

            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        catch (Exception ex)
        {
            message.Status = AiJobStatus.Failed;
            message.ErrorMessage = ex.Message;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
