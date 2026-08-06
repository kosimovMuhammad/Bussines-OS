using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
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
    IQuotationPdfRenderer pdfRenderer) : IAiService
{
    public async Task<QueuedResponse> SummarizeAsync(string companyId, SummarizeRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await db.Communications.AnyAsync(c => c.Id == request.CommunicationId, cancellationToken);
        if (!exists) throw new NotFoundException("Communication not found");

        await queue.QueueAsync(async (sp, ct) => await sp.GetRequiredService<IAiService>().ProcessSummarizeAsync(request.CommunicationId, ct));
        return new QueuedResponse(true, request.CommunicationId);
    }

    public async Task<AiSummaryDto> GetSummaryAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var summary = await db.AiSummaries.SingleOrDefaultAsync(s => s.Id == id || s.CommunicationId == id, cancellationToken)
            ?? throw new NotFoundException("AI summary not found");

        return new AiSummaryDto(summary.Id, summary.CommunicationId, summary.Summary, summary.Sentiment?.ToString(), summary.ActionItemsJson, summary.NextFollowUp, summary.GeneratedAt);
    }

    public async Task<QueuedResponse> QueryAsync(string companyId, AiQueryRequest request, CancellationToken cancellationToken = default)
    {
        var result = new AiQueryResult { CompanyId = companyId, Question = request.Question };
        db.AiQueryResults.Add(result);
        await db.SaveChangesAsync(cancellationToken);

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

    public async Task<QueuedResponse> DraftEmailAsync(string companyId, AiDraftEmailRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        var draft = new AiEmailDraft { CompanyId = companyId, ContactId = contact.Id, Context = request.Context };
        db.AiEmailDrafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);

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
        var deal = await db.Deals.SingleOrDefaultAsync(d => d.Id == request.DealId, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        var itemsJson = JsonSerializer.Serialize(request.Items);
        var quotation = new AiQuotation { CompanyId = companyId, DealId = deal.Id, ItemsJson = itemsJson };
        db.AiQuotations.Add(quotation);
        await db.SaveChangesAsync(cancellationToken);

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

            var rowsJson = JsonSerializer.Serialize(rows);
            var answerPrompt = $"In one sentence, answer the question '{result.Question}' using this data: {rowsJson}";
            var answer = await gemini.GenerateAsync(answerPrompt, maxOutputTokens: 256, cancellationToken: cancellationToken);

            result.Intent = intent;
            result.ResultRowsJson = rowsJson;
            result.Answer = answer.Trim();
            result.Status = AiJobStatus.Completed;
            result.CompletedAt = DateTime.UtcNow;
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

        try
        {
            var items = JsonSerializer.Deserialize<List<QuotationItemRequest>>(quotation.ItemsJson) ?? [];
            var total = items.Sum(i => i.Quantity * i.UnitPrice);
            var pdfItems = items.Select(i => new QuotationLineItem(i.Description, i.Quantity, i.UnitPrice)).ToList();
            var pdfBytes = pdfRenderer.Render(quotation.Deal.Title, pdfItems, total, quotation.Deal.Currency);

            var key = storage.BuildKey(quotation.CompanyId, $"quotation-{quotation.Deal.Title}.pdf");
            await storage.UploadAsync(key, pdfBytes, "application/pdf", cancellationToken);

            var fileAsset = new FileAsset
            {
                CompanyId = quotation.CompanyId,
                DealId = quotation.DealId,
                UploadedById = quotation.Deal.OwnerId ?? quotation.Deal.CompanyId,
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
        }
        catch (Exception ex)
        {
            quotation.Status = AiJobStatus.Failed;
            quotation.ErrorMessage = ex.Message;
            quotation.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<object> RunStaleContactsAsync(string companyId, int days, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var contacts = await db.Contacts
            .Where(c => !db.Communications.Any(cm => cm.ContactId == c.Id && cm.Timestamp >= cutoff))
            .Select(c => new { c.Id, Name = $"{c.FirstName} {c.LastName}", c.Email, c.Phone })
            .Take(50)
            .ToListAsync(cancellationToken);
        return contacts;
    }

    private async Task<object> RunTopSalespersonAsync(string companyId, CancellationToken cancellationToken)
    {
        var results = await db.Deals
            .Where(d => d.Stage == DealStage.Won && d.OwnerId != null)
            .GroupBy(d => d.OwnerId)
            .Select(g => new { OwnerId = g.Key, TotalValue = g.Sum(d => d.Value), Count = g.Count() })
            .OrderByDescending(g => g.TotalValue)
            .Take(5)
            .ToListAsync(cancellationToken);

        var ownerIds = results.Select(r => r.OwnerId).ToList();
        var users = await db.Users.Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        return results.Select(r => new { Name = users.GetValueOrDefault(r.OwnerId!, "Unknown"), r.TotalValue, r.Count }).ToList();
    }

    private async Task<object> RunRevenueByPeriodAsync(string companyId, int months, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-months);
        var deals = await db.Deals
            .Where(d => d.Stage == DealStage.Won && d.UpdatedAt >= cutoff)
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

    private static AiQueryResultDto ToQueryDto(AiQueryResult r) => new(r.Id, r.Question, r.Intent?.ToString(), r.Status.ToString(), r.ResultRowsJson, r.Answer, r.ErrorMessage);
}
