using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Activity;
using BusinessOS.Application.ViewModels.Deals;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class DealsService(IAppDbContext db, INotificationService notifications, ILimitEnforcementService limitEnforcement, IActivityLogService activityLog) : IDealsService
{
    public async Task<IReadOnlyList<DealDto>> ListAsync(string companyId, QueryDealsRequest query, CancellationToken cancellationToken = default)
    {
        var deals = db.Deals.Include(d => d.Contact).Include(d => d.Owner).AsQueryable();

        if (query.Stage is not null)
        {
            deals = deals.Where(d => d.Stage == query.Stage);
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerId))
        {
            deals = deals.Where(d => d.OwnerId == query.OwnerId);
        }

        if (!query.IncludeArchived)
        {
            deals = deals.Where(d => !d.IsArchived);
        }

        var items = await deals.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(string companyId, string id, CancellationToken cancellationToken = default) =>
        activityLog.GetDealTimelineAsync(companyId, id, cancellationToken);

    public async Task<DealDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var deal = await db.Deals
            .Include(d => d.Contact)
            .Include(d => d.Owner)
            .Include(d => d.Tasks)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        return new DealDetailDto(
            deal.Id, deal.CompanyId, deal.ContactId, $"{deal.Contact.FirstName} {deal.Contact.LastName}".Trim(),
            deal.Title, deal.Value, deal.Currency, deal.Stage.ToString(), deal.Probability, deal.ExpectedCloseDate,
            deal.OwnerId, deal.Owner?.Name, deal.CreatedAt, deal.UpdatedAt,
            deal.Tasks.Select(t => new DealTaskSummary(t.Id, t.Title, t.Status.ToString(), t.DueDate)).ToList());
    }

    public async Task<DealDto> CreateAsync(string companyId, CreateDealRequest request, CancellationToken cancellationToken = default)
    {
        await limitEnforcement.CheckLimitAsync(companyId, UsageMetricKeys.MaxDeals, cancellationToken);

        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        if (request.OwnerId is not null)
        {
            await ValidateOwnerAsync(companyId, request.OwnerId, cancellationToken);
        }

        var deal = new Deal
        {
            CompanyId = companyId,
            ContactId = request.ContactId,
            Title = request.Title,
            Value = request.Value,
            Currency = request.Currency,
            Stage = request.Stage,
            Probability = request.Probability ?? ResolveProbability(request.Stage),
            ExpectedCloseDate = request.ExpectedCloseDate,
            OwnerId = request.OwnerId,
        };

        db.Deals.Add(deal);
        await db.SaveChangesAsync(cancellationToken);

        deal.Contact = contact;

        if (deal.OwnerId is not null)
        {
            await notifications.NotifyAsync(companyId, deal.OwnerId, NotificationType.DealAssigned,
                "Deal assigned to you", $"\"{deal.Title}\" was assigned to you", "Deal", deal.Id, cancellationToken);
        }

        return ToDto(deal);
    }

    public async Task<DealDto> UpdateAsync(string companyId, string id, UpdateDealRequest request, CancellationToken cancellationToken = default)
    {
        var deal = await db.Deals.Include(d => d.Contact).Include(d => d.Owner).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        if (request.ContactId is not null)
        {
            var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
                ?? throw new NotFoundException("Contact not found");
            deal.ContactId = request.ContactId;
            deal.Contact = contact;
        }

        var previousOwnerId = deal.OwnerId;
        if (request.OwnerId is not null)
        {
            await ValidateOwnerAsync(companyId, request.OwnerId, cancellationToken);
            deal.OwnerId = request.OwnerId;
        }

        if (request.Title is not null) deal.Title = request.Title;
        if (request.Value is not null) deal.Value = request.Value.Value;
        if (request.Currency is not null) deal.Currency = request.Currency;
        if (request.Probability is not null) deal.Probability = request.Probability.Value;
        if (request.ExpectedCloseDate is not null) deal.ExpectedCloseDate = request.ExpectedCloseDate;

        await db.SaveChangesAsync(cancellationToken);

        if (request.OwnerId is not null && request.OwnerId != previousOwnerId)
        {
            await notifications.NotifyAsync(companyId, request.OwnerId, NotificationType.DealAssigned,
                "Deal assigned to you", $"\"{deal.Title}\" was assigned to you", "Deal", deal.Id, cancellationToken);
        }

        return ToDto(deal);
    }

    public async Task<DealDto> UpdateStageAsync(string companyId, string id, UpdateDealStageRequest request, CancellationToken cancellationToken = default)
    {
        var deal = await db.Deals.Include(d => d.Contact).Include(d => d.Owner).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        var previousStage = deal.Stage;
        deal.Stage = request.Stage;
        deal.Probability = ResolveProbability(request.Stage, deal.Probability);

        await db.SaveChangesAsync(cancellationToken);

        if (deal.OwnerId is not null && previousStage != deal.Stage)
        {
            await notifications.NotifyAsync(companyId, deal.OwnerId, NotificationType.DealStageChanged,
                "Deal stage changed", $"\"{deal.Title}\" moved to {deal.Stage}", "Deal", deal.Id, cancellationToken);
        }

        return ToDto(deal);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var deal = await db.Deals.SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        db.Deals.Remove(deal);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateOwnerAsync(string companyId, string ownerId, CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == ownerId && u.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Owner not found in this company");
        }
    }

    private static int ResolveProbability(DealStage stage, int? current = null) => stage switch
    {
        DealStage.Won => 100,
        DealStage.Lost => 0,
        _ => current ?? 0,
    };

    private static DealDto ToDto(Deal d) => new(
        d.Id, d.CompanyId, d.ContactId, $"{d.Contact.FirstName} {d.Contact.LastName}".Trim(), d.Title, d.Value, d.Currency,
        d.Stage.ToString(), d.Probability, d.ExpectedCloseDate, d.OwnerId, d.Owner?.Name, d.CreatedAt, d.UpdatedAt, d.IsArchived);

    public async Task<BulkOperationResult> BulkUpdateAsync(string companyId, IReadOnlyList<string> ids, UpdateDealRequest patch, CancellationToken cancellationToken = default)
    {
        var results = new List<BulkOperationResultItem>();
        foreach (var id in ids)
        {
            try
            {
                await UpdateAsync(companyId, id, patch, cancellationToken);
                results.Add(new BulkOperationResultItem(id, true, null));
            }
            catch (Exception ex) when (ex is NotFoundException or ConflictException or ValidationAppException or ForbiddenException)
            {
                results.Add(new BulkOperationResultItem(id, false, ex.Message));
            }
        }

        return BulkOperationResult.From(results);
    }

    public async Task<BulkOperationResult> BulkDeleteAsync(string companyId, IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        var results = new List<BulkOperationResultItem>();
        foreach (var id in ids)
        {
            try
            {
                await RemoveAsync(companyId, id, cancellationToken);
                results.Add(new BulkOperationResultItem(id, true, null));
            }
            catch (Exception ex) when (ex is NotFoundException or ConflictException or ValidationAppException or ForbiddenException)
            {
                results.Add(new BulkOperationResultItem(id, false, ex.Message));
            }
        }

        return BulkOperationResult.From(results);
    }

    public async Task<string> ExportCsvAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var deals = await db.Deals.Include(d => d.Contact).Where(d => d.CompanyId == companyId).OrderBy(d => d.CreatedAt).ToListAsync(cancellationToken);

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("ContactEmail,Title,Value,Currency,Stage");
        foreach (var d in deals)
        {
            csv.AppendLine(string.Join(',', CsvUtils.Field(d.Contact.Email), CsvUtils.Field(d.Title), CsvUtils.Field(d.Value.ToString()), CsvUtils.Field(d.Currency), CsvUtils.Field(d.Stage.ToString())));
        }

        return csv.ToString();
    }

    // Dedup key is (ContactId, Title) exact match - Deal has no natural business key of its
    // own (unlike Contact's Email), so this is the closest reasonable stand-in. ContactEmail
    // must resolve to an existing Contact in this company or the row errors (no
    // create-a-contact-on-the-fly side effect from a Deal import).
    public async Task<ImportResultDto> ImportCsvAsync(string companyId, string csvContent, CancellationToken cancellationToken = default)
    {
        var rows = CsvUtils.ParseRows(csvContent);
        if (rows.Count == 0)
        {
            return new ImportResultDto(0, 0, []);
        }

        var contactsByEmail = await db.Contacts.Where(c => c.CompanyId == companyId && c.Email != null)
            .ToDictionaryAsync(c => c.Email!.ToLower(), cancellationToken);

        var existingTitlesByContact = (await db.Deals.Where(d => d.CompanyId == companyId).Select(d => new { d.ContactId, d.Title }).ToListAsync(cancellationToken))
            .ToHashSet();

        var imported = 0;
        var skipped = 0;
        var errors = new List<ImportErrorDto>();

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 1;

            if (row.Length < 2 || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]))
            {
                errors.Add(new ImportErrorDto(rowNumber, "ContactEmail and Title are required"));
                continue;
            }

            if (!contactsByEmail.TryGetValue(row[0].ToLower(), out var contact))
            {
                errors.Add(new ImportErrorDto(rowNumber, $"No contact found with email '{row[0]}'"));
                continue;
            }

            var title = row[1];
            if (existingTitlesByContact.Any(x => x.ContactId == contact.Id && x.Title == title))
            {
                skipped++;
                continue;
            }

            var value = row.Length > 2 && decimal.TryParse(row[2], out var parsedValue) ? parsedValue : 0m;
            var currency = row.Length > 3 && !string.IsNullOrWhiteSpace(row[3]) ? row[3] : "TJS";
            var stage = row.Length > 4 && Enum.TryParse<DealStage>(row[4], true, out var parsedStage) ? parsedStage : DealStage.Lead;

            db.Deals.Add(new Deal
            {
                CompanyId = companyId,
                ContactId = contact.Id,
                Title = title,
                Value = value,
                Currency = currency,
                Stage = stage,
                Probability = ResolveProbability(stage),
            });

            existingTitlesByContact.Add(new { ContactId = contact.Id, Title = title });
            imported++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportResultDto(imported, skipped, errors);
    }
}
