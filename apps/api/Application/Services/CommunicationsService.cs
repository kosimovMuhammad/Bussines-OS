using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Communications;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class CommunicationsService(IAppDbContext db) : ICommunicationsService
{
    public async Task<IReadOnlyList<CommunicationDto>> ListAsync(string companyId, QueryCommunicationsRequest query, CancellationToken cancellationToken = default)
    {
        var communications = db.Communications.Include(c => c.Contact).Include(c => c.AiSummary).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ContactId))
        {
            communications = communications.Where(c => c.ContactId == query.ContactId);
        }
        if (query.Channel is not null)
        {
            communications = communications.Where(c => c.Channel == query.Channel);
        }

        var items = await communications.OrderByDescending(c => c.Timestamp).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<CommunicationDto> CreateNoteAsync(string companyId, CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        var communication = new Communication
        {
            CompanyId = companyId,
            ContactId = request.ContactId,
            Channel = CommunicationChannel.ManualNote,
            Direction = request.Direction,
            Content = request.Content,
            Status = CommunicationStatus.Sent,
        };

        db.Communications.Add(communication);
        await db.SaveChangesAsync(cancellationToken);

        communication.Contact = contact;
        return ToDto(communication);
    }

    private static CommunicationDto ToDto(Communication c) => new(
        c.Id, c.CompanyId, c.ContactId,
        c.Contact is null ? null : $"{c.Contact.FirstName} {c.Contact.LastName}".Trim(),
        c.Channel.ToString(), c.Direction.ToString(), c.Content, c.ProviderMessageId, c.Status.ToString(), c.Timestamp,
        c.AiSummary?.Summary);
}
