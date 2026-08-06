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

public class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty();
    }
}
