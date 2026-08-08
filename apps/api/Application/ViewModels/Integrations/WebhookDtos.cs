using FluentValidation;

namespace BusinessOS.Application.ViewModels.Integrations;

public record WebhookDto(
    string Id,
    string Url,
    IReadOnlyList<string> SubscribedEvents,
    bool IsActive,
    bool HasSecret,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// Every other read (ListAsync, GetAsync) exposes HasSecret only - same one-time-reveal
// convention as ApiKeysService.CreateAsync's RawKey.
public record CreateWebhookResponse(string Id, string Url, IReadOnlyList<string> SubscribedEvents, bool IsActive, string Secret, DateTime CreatedAt);

public record CreateWebhookRequest(string Url, List<string> SubscribedEvents, bool IsActive = true);

public record UpdateWebhookRequest(string? Url, List<string>? SubscribedEvents, bool? IsActive);

public record WebhookDeliveryLogDto(string Id, string? WebhookId, string Event, string Url, int? StatusCode, bool Success, string? ErrorMessage, DateTime CreatedAt);

public record QueryWebhookLogsRequest(string? WebhookId, bool? Success, int Page = 1, int Limit = 20);

public class CreateWebhookRequestValidator : AbstractValidator<CreateWebhookRequest>
{
    public CreateWebhookRequestValidator()
    {
        RuleFor(x => x.Url).NotEmpty().Must(u => Uri.TryCreate(u, UriKind.Absolute, out _)).WithMessage("Url must be an absolute URL");
        RuleFor(x => x.SubscribedEvents).NotEmpty().WithMessage("At least one subscribed event is required");
    }
}

public class UpdateWebhookRequestValidator : AbstractValidator<UpdateWebhookRequest>
{
    public UpdateWebhookRequestValidator()
    {
        RuleFor(x => x.Url).Must(u => Uri.TryCreate(u, UriKind.Absolute, out _)).WithMessage("Url must be an absolute URL").When(x => x.Url is not null);
        RuleFor(x => x.SubscribedEvents).NotEmpty().WithMessage("At least one subscribed event is required").When(x => x.SubscribedEvents is not null);
    }
}
