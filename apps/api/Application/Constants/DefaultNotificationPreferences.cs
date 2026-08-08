using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Constants;

// Code-level fallback used whenever no NotificationPreference override row exists for a
// given (Type, Channel) pair - see NotificationPreference.cs for why overrides are sparse.
// InApp/Email default on for everything (no notification type here is "marketing" in the
// sense the original requirements warned against); WhatsApp defaults off since it's the
// most intrusive/costly channel to send on - users opt in per type.
public static class DefaultNotificationPreferences
{
    public static bool IsEnabledByDefault(NotificationType type, NotificationChannel channel) => channel switch
    {
        NotificationChannel.InApp => true,
        NotificationChannel.Email => true,
        NotificationChannel.WhatsApp => false,
        _ => false,
    };
}
