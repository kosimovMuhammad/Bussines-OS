using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Activity;
using BusinessOS.Application.ViewModels.Contacts;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class ContactsService(IAppDbContext db, ILimitEnforcementService limitEnforcement, IActivityLogService activityLog) : IContactsService
{
    public async Task<PagedResult<ContactDto>> ListAsync(string companyId, QueryContactsRequest query, CancellationToken cancellationToken = default)
    {
        var contacts = db.Contacts.Include(c => c.Owner).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            contacts = contacts.Where(c =>
                c.FirstName.Contains(term) ||
                (c.LastName != null && c.LastName.Contains(term)) ||
                (c.Email != null && c.Email.Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            contacts = contacts.Where(c => c.Tags.Contains(query.Tag));
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerId))
        {
            contacts = contacts.Where(c => c.OwnerId == query.OwnerId);
        }

        if (!query.IncludeArchived)
        {
            contacts = contacts.Where(c => !c.IsArchived);
        }

        var total = await contacts.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await contacts
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<ContactDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<ContactDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts
            .Include(c => c.Owner)
            .Include(c => c.Deals)
            .Include(c => c.Tasks)
            .Include(c => c.Communications).ThenInclude(cm => cm.AiSummary)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        return new ContactDetailDto(
            contact.Id, contact.CompanyId, contact.FirstName, contact.LastName, contact.Email, contact.Phone,
            contact.Position, contact.CompanyName, contact.Notes, contact.OwnerId, contact.Owner?.Name, contact.Tags,
            contact.CreatedAt, contact.UpdatedAt,
            contact.Deals.Select(d => new ContactDealSummary(d.Id, d.Title, d.Stage.ToString(), d.Value, d.Currency)).ToList(),
            contact.Tasks.Select(t => new ContactTaskSummary(t.Id, t.Title, t.Status.ToString(), t.DueDate)).ToList(),
            contact.Communications
                .OrderByDescending(cm => cm.Timestamp)
                .Select(ToCommunicationSummary)
                .ToList());
    }

    public Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(string companyId, string id, CancellationToken cancellationToken = default) =>
        activityLog.GetContactTimelineAsync(companyId, id, cancellationToken);

    public async Task<ContactDto> CreateAsync(string companyId, CreateContactRequest request, CancellationToken cancellationToken = default)
    {
        await limitEnforcement.CheckLimitAsync(companyId, UsageMetricKeys.MaxContacts, cancellationToken);
        await ValidateOwnerAsync(companyId, request.OwnerId, cancellationToken);

        var contact = new Contact
        {
            CompanyId = companyId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = PhoneUtil.Normalize(request.Phone),
            Position = request.Position,
            CompanyName = request.CompanyName,
            Notes = request.Notes,
            OwnerId = request.OwnerId,
            Tags = request.Tags ?? [],
        };

        db.Contacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);

        contact.Owner = contact.OwnerId is null ? null : await db.Users.FindAsync([contact.OwnerId], cancellationToken);
        return ToDto(contact);
    }

    public async Task<ContactDto> UpdateAsync(string companyId, string id, UpdateContactRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.Include(c => c.Owner).SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        if (request.OwnerId is not null)
        {
            await ValidateOwnerAsync(companyId, request.OwnerId, cancellationToken);
        }

        if (request.FirstName is not null) contact.FirstName = request.FirstName;
        if (request.LastName is not null) contact.LastName = request.LastName;
        if (request.Email is not null) contact.Email = request.Email;
        if (request.Phone is not null) contact.Phone = PhoneUtil.Normalize(request.Phone);
        if (request.Position is not null) contact.Position = request.Position;
        if (request.CompanyName is not null) contact.CompanyName = request.CompanyName;
        if (request.Notes is not null) contact.Notes = request.Notes;
        if (request.OwnerId is not null) contact.OwnerId = request.OwnerId;
        if (request.Tags is not null) contact.Tags = request.Tags;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(contact);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        var hasDeals = await db.Deals.AnyAsync(d => d.ContactId == id, cancellationToken);
        var hasInvoices = await db.Invoices.AnyAsync(i => i.ContactId == id, cancellationToken);
        if (hasDeals || hasInvoices)
        {
            throw new ConflictException("Cannot delete a contact with linked deals or invoices");
        }

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateOwnerAsync(string companyId, string? ownerId, CancellationToken cancellationToken)
    {
        if (ownerId is null)
        {
            return;
        }

        var exists = await db.Users.AnyAsync(u => u.Id == ownerId && u.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Owner not found in this company");
        }
    }

    private static ContactCommunicationSummary ToCommunicationSummary(Communication cm) => new(
        cm.Id, cm.Channel.ToString(), cm.Direction.ToString(), cm.Content, cm.Status.ToString(), cm.Timestamp, cm.AiSummary?.Summary);

    private static ContactDto ToDto(Contact c) => new(
        c.Id, c.CompanyId, c.FirstName, c.LastName, c.Email, c.Phone, c.Position, c.CompanyName, c.Notes,
        c.OwnerId, c.Owner?.Name, c.Tags, c.CreatedAt, c.UpdatedAt, c.IsArchived);

    public async Task<BulkOperationResult> BulkUpdateAsync(string companyId, IReadOnlyList<string> ids, UpdateContactRequest patch, CancellationToken cancellationToken = default)
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
        var contacts = await db.Contacts.Where(c => c.CompanyId == companyId).OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("FirstName,LastName,Email,Phone,Position,CompanyName,Notes");
        foreach (var c in contacts)
        {
            csv.AppendLine(string.Join(',',
                CsvUtils.Field(c.FirstName), CsvUtils.Field(c.LastName), CsvUtils.Field(c.Email), CsvUtils.Field(c.Phone),
                CsvUtils.Field(c.Position), CsvUtils.Field(c.CompanyName), CsvUtils.Field(c.Notes)));
        }

        return csv.ToString();
    }

    // Dedup by Email (case-insensitive) within the company - a row whose Email already
    // exists is Skipped, not treated as an error, per the existing product decision on
    // default import dedup strategy. Rows with no Email (or a FirstName-less row) can't be
    // deduped meaningfully and are always imported.
    public async Task<ImportResultDto> ImportCsvAsync(string companyId, string csvContent, CancellationToken cancellationToken = default)
    {
        var rows = CsvUtils.ParseRows(csvContent);
        if (rows.Count == 0)
        {
            return new ImportResultDto(0, 0, []);
        }

        var existingEmails = (await db.Contacts.Where(c => c.CompanyId == companyId && c.Email != null)
            .Select(c => c.Email!.ToLower()).ToListAsync(cancellationToken)).ToHashSet();

        var imported = 0;
        var skipped = 0;
        var errors = new List<ImportErrorDto>();

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 1;

            if (row.Length == 0 || string.IsNullOrWhiteSpace(row[0]))
            {
                errors.Add(new ImportErrorDto(rowNumber, "FirstName is required"));
                continue;
            }

            var email = row.Length > 2 ? row[2] : null;
            if (!string.IsNullOrWhiteSpace(email) && existingEmails.Contains(email.ToLower()))
            {
                skipped++;
                continue;
            }

            var contact = new Contact
            {
                CompanyId = companyId,
                FirstName = row[0],
                LastName = row.Length > 1 ? EmptyToNull(row[1]) : null,
                Email = EmptyToNull(email),
                Phone = row.Length > 3 ? PhoneUtil.Normalize(EmptyToNull(row[3])) : null,
                Position = row.Length > 4 ? EmptyToNull(row[4]) : null,
                CompanyName = row.Length > 5 ? EmptyToNull(row[5]) : null,
                Notes = row.Length > 6 ? EmptyToNull(row[6]) : null,
            };

            db.Contacts.Add(contact);
            if (!string.IsNullOrWhiteSpace(email))
            {
                existingEmails.Add(email.ToLower());
            }

            imported++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportResultDto(imported, skipped, errors);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
