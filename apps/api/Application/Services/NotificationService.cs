using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Notifications;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Services;

public class NotificationService(
    IAppDbContext db,
    IAuditLogService auditLog,
    IBackgroundTaskQueue backgroundQueue,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<PagedResult<NotificationDto>> ListAsync(string userId, QueryNotificationsRequest query, CancellationToken cancellationToken = default)
    {
        var source = db.Notifications.Where(n => n.UserId == userId);

        if (query.IsRead is not null)
        {
            source = source.Where(n => n.IsRead == query.IsRead);
        }

        var total = await source.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await source
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<NotificationDto> GetAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Notification not found");
        return ToDto(notification);
    }

    public async Task<NotificationDto> CreateAsync(string companyId, string actorUserId, CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var targetExists = await db.Users.AnyAsync(u => u.Id == request.TargetUserId && u.CompanyId == companyId, cancellationToken);
        if (!targetExists)
        {
            throw new NotFoundException("Target user not found in this company");
        }

        var notification = new Notification
        {
            CompanyId = companyId,
            UserId = request.TargetUserId,
            Type = request.Type,
            Title = request.Title,
            Body = request.Body,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
        };

        db.Notifications.Add(notification);
        auditLog.Log(companyId, actorUserId, "notification.created", "Notification", notification.Id, new { targetUserId = request.TargetUserId, type = request.Type.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(notification);
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
        await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public async Task MarkReadAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Notification not found");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Notification not found");

        db.Notifications.Remove(notification);
        auditLog.Log(notification.CompanyId, userId, "notification.deleted", "Notification", id, new { type = notification.Type.ToString() });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyAsync(string companyId, string userId, NotificationType type, string title, string body, string? relatedEntityType = null, string? relatedEntityId = null, CancellationToken cancellationToken = default)
    {
        var inAppEnabled = await IsChannelEnabledAsync(userId, type, NotificationChannel.InApp, cancellationToken);
        if (inAppEnabled)
        {
            db.Notifications.Add(new Notification
            {
                CompanyId = companyId,
                UserId = userId,
                Type = type,
                Title = title,
                Body = body,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        var emailEnabled = await IsChannelEnabledAsync(userId, type, NotificationChannel.Email, cancellationToken);
        if (emailEnabled)
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is not null)
            {
                var recipientEmail = user.Email;
                var recipientName = user.Name;
                await backgroundQueue.QueueAsync(async (sp, ct) =>
                {
                    try
                    {
                        var mail = sp.GetRequiredService<IMailService>();
                        await mail.SendNotificationEmailAsync(recipientEmail, recipientName, title, body, ct);
                    }
                    catch (Exception ex)
                    {
                        var bgLogger = sp.GetRequiredService<ILogger<NotificationService>>();
                        bgLogger.LogError(ex, "Failed to send notification email to user {UserId}", userId);
                    }
                });
            }
        }

        var whatsAppEnabled = await IsChannelEnabledAsync(userId, type, NotificationChannel.WhatsApp, cancellationToken);
        if (whatsAppEnabled)
        {
            // TODO: no User.Phone field exists yet - once one is added, resolve the recipient's
            // phone number here and call IWhatsAppClient.SendPlatformTextMessageAsync, dispatched
            // via IBackgroundTaskQueue the same way the Email branch above does.
            logger.LogInformation("WhatsApp notification dispatch not yet implemented - no User.Phone source (userId={UserId}, type={Type})", userId, type);
        }
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var overrides = await db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => (p.Type, p.Channel), p => p.Enabled, cancellationToken);

        var result = new List<NotificationPreferenceDto>();
        foreach (var type in Enum.GetValues<NotificationType>())
        {
            foreach (var channel in Enum.GetValues<NotificationChannel>())
            {
                var enabled = overrides.TryGetValue((type, channel), out var overridden)
                    ? overridden
                    : DefaultNotificationPreferences.IsEnabledByDefault(type, channel);
                result.Add(new NotificationPreferenceDto(type.ToString(), channel.ToString(), enabled));
            }
        }

        return result;
    }

    public async Task UpdatePreferencesAsync(string companyId, string userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        foreach (var item in request.Preferences)
        {
            var existing = await db.NotificationPreferences
                .SingleOrDefaultAsync(p => p.UserId == userId && p.Type == item.Type && p.Channel == item.Channel, cancellationToken);

            var isDefault = item.Enabled == DefaultNotificationPreferences.IsEnabledByDefault(item.Type, item.Channel);

            if (isDefault)
            {
                // Matches the code default - no need to keep an override row (sparse storage).
                if (existing is not null)
                {
                    db.NotificationPreferences.Remove(existing);
                }

                continue;
            }

            if (existing is not null)
            {
                existing.Enabled = item.Enabled;
            }
            else
            {
                db.NotificationPreferences.Add(new NotificationPreference
                {
                    CompanyId = companyId,
                    UserId = userId,
                    Type = item.Type,
                    Channel = item.Channel,
                    Enabled = item.Enabled,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> IsChannelEnabledAsync(string userId, NotificationType type, NotificationChannel channel, CancellationToken cancellationToken)
    {
        var overridden = await db.NotificationPreferences
            .Where(p => p.UserId == userId && p.Type == type && p.Channel == channel)
            .Select(p => (bool?)p.Enabled)
            .SingleOrDefaultAsync(cancellationToken);

        return overridden ?? DefaultNotificationPreferences.IsEnabledByDefault(type, channel);
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.Id, n.Type.ToString(), n.Title, n.Body, n.RelatedEntityType, n.RelatedEntityId, n.IsRead, n.CreatedAt, n.ReadAt);
}
