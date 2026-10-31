using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class WhatsAppAccount : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string WabaId { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string? VerifiedName { get; set; }
    public bool IsActive { get; set; } = true;

    // Long-lived tokens from the fb_exchange_token grant are valid for ~60 days and must be
    // refreshed before they expire - null for tokens connected via the legacy manual-paste
    // path where the expiry isn't known (see InstagramAccount.TokenExpiresAt for the same
    // reasoning, and WhatsAppTokenRefreshService for the refresh job).
    public DateTime? TokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
