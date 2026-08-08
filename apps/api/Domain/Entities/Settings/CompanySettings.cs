using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// One row per company, created lazily on first read/write (see SettingsService). Company's
// own Name/Timezone fields stay on Company itself (pre-existing, used by /company too) -
// this table only holds the fields that had nowhere to live before Settings Phase 3.
public class CompanySettings : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;

    public string Locale { get; set; } = "en";
    public string Currency { get; set; } = "TJS";

    public int PasswordMinLength { get; set; } = 8;
    public bool PasswordRequireUppercase { get; set; } = true;
    public bool PasswordRequireNumber { get; set; } = true;
    public bool PasswordRequireSymbol { get; set; }
    public int SessionTimeoutMinutes { get; set; } = 60;
    public bool RequireTwoFactor { get; set; }

    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#2563EB";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
