using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Contacts;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class ContactsService(IAppDbContext db) : IContactsService
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

    public async Task<IReadOnlyList<ContactCommunicationSummary>> GetTimelineAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var exists = await db.Contacts.AnyAsync(c => c.Id == id, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Contact not found");
        }

        var communications = await db.Communications
            .Include(cm => cm.AiSummary)
            .Where(cm => cm.ContactId == id)
            .OrderBy(cm => cm.Timestamp)
            .ToListAsync(cancellationToken);

        return communications.Select(ToCommunicationSummary).ToList();
    }

    public async Task<ContactDto> CreateAsync(string companyId, CreateContactRequest request, CancellationToken cancellationToken = default)
    {
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
        c.OwnerId, c.Owner?.Name, c.Tags, c.CreatedAt, c.UpdatedAt);
}
