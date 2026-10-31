using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class InstagramAccount : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string InstagramBusinessAccountId { get; set; } = string.Empty;
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
