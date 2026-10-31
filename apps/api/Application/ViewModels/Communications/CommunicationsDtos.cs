using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Communications;

public record CreateNoteRequest(string ContactId, string Content, CommunicationDirection Direction = CommunicationDirection.Outbound);

public record QueryCommunicationsRequest(string? ContactId, CommunicationChannel? Channel);

public record CommunicationDto(
    string Id,
    string CompanyId,
    string? ContactId,
    string? ContactName,
    string Channel,
    string Direction,
    string Content,
    string? ProviderMessageId,
    string Status,
    DateTime Timestamp,
    string? AiSummary);

// Shared by every channel service (Instagram/Telegram/WhatsApp/CommunicationsService) so the
// mapping to the wire DTO - including the real-time push payload, see IRealtimeNotifier - stays
// in one place. contact is a separate param (not just c.Contact) because most of the inbound
// webhook paths don't eager-load the Contact navigation onto the Communication they just built.
public static class CommunicationDtoMapper
{
    public static CommunicationDto ToDto(Communication c, Contact? contact = null)
    {
        var resolvedContact = contact ?? c.Contact;
        return new CommunicationDto(
            c.Id, c.CompanyId, c.ContactId,
            resolvedContact is null ? null : $"{resolvedContact.FirstName} {resolvedContact.LastName}".Trim(),
            c.Channel.ToString(), c.Direction.ToString(), c.Content, c.ProviderMessageId, c.Status.ToString(), c.Timestamp,
            c.AiSummary?.Summary);
    }
}

public class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty();
    }
}
