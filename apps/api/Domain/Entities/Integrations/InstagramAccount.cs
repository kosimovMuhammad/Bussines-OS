using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class InstagramAccount : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    // The Instagram PROFESSIONAL account id (<IG_ID>): /me "user_id". This is the value Meta puts
    // in webhook entry.id / messaging.recipient.id and what messages / subscribed_apps use.
    public string InstagramBusinessAccountId { get; set; } = string.Empty;

    // The app-scoped user id: /me "id". Never appears in webhooks; kept so reconnects, legacy rows
    // (which stored this value in InstagramBusinessAccountId) and Meta's deauthorize /
    // data-deletion callbacks can still be matched to the account. Null = not yet backfilled.
    public string? AppScopedUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Long-lived Instagram access tokens are valid for ~60 days and must be refreshed before
    // they expire - nullable because we may not always know the expiry (e.g. token pasted
    // manually rather than obtained through the long-lived-token exchange flow).
    public DateTime? TokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
