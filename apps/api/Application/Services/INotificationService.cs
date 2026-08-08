using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Notifications;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> ListAsync(string userId, QueryNotificationsRequest query, CancellationToken cancellationToken = default);
    Task<NotificationDto> GetAsync(string userId, string id, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
    Task<NotificationDto> CreateAsync(string companyId, CreateNotificationRequest request, CancellationToken cancellationToken = default);
    Task MarkReadAsync(string userId, string id, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(string userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);

    // Entry point other services call to raise a notification. Gated by the recipient's
    // InApp preference for this Type - a disabled preference means no row is written at
    // all, not a written-but-suppressed one.
    Task NotifyAsync(string companyId, string userId, NotificationType type, string title, string body, string? relatedEntityType = null, string? relatedEntityId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(string userId, CancellationToken cancellationToken = default);
    Task UpdatePreferencesAsync(string companyId, string userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default);
}
