using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Sparse overrides only - a row exists here only when the user deviated from the
// code-level default (see DefaultNotificationPreferences, added alongside the
// preferences service). No row means "use the default for this Type+Channel".
public class NotificationPreference : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }
    public bool Enabled { get; set; }
}
