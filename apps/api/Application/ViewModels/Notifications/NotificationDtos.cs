using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Notifications;

public record NotificationDto(
    string Id,
    string Type,
    string Title,
    string Body,
    string? RelatedEntityType,
    string? RelatedEntityId,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt);

public record QueryNotificationsRequest(bool? IsRead = null, int Page = 1, int Limit = 20);

// Explicit admin action, not a system trigger - always written regardless of the target
// user's InApp preference (preferences only govern automated NotifyAsync triggers).
public record CreateNotificationRequest(string TargetUserId, NotificationType Type, string Title, string Body, string? RelatedEntityType = null, string? RelatedEntityId = null);

public class CreateNotificationRequestValidator : AbstractValidator<CreateNotificationRequest>
{
    public CreateNotificationRequestValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
    }
}

public record NotificationPreferenceDto(string Type, string Channel, bool Enabled);

public record NotificationPreferenceItem(NotificationType Type, NotificationChannel Channel, bool Enabled);

public record UpdateNotificationPreferencesRequest(IReadOnlyList<NotificationPreferenceItem> Preferences);

public class UpdateNotificationPreferencesRequestValidator : AbstractValidator<UpdateNotificationPreferencesRequest>
{
    public UpdateNotificationPreferencesRequestValidator()
    {
        RuleFor(x => x.Preferences).NotNull();
        RuleForEach(x => x.Preferences).ChildRules(item =>
        {
            item.RuleFor(i => i.Type).IsInEnum();
            item.RuleFor(i => i.Channel).IsInEnum();
        });
    }
}
