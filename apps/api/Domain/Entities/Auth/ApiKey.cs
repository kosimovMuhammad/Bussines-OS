using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Only the SHA-256 hash of the raw key is ever persisted - the raw value is returned to the
// caller exactly once, at creation, and is unrecoverable after that (same "can't be read
// back" guarantee as a password, but hashed with SHA-256 rather than Argon2: the key is
// already a high-entropy random value, not a low-entropy user-chosen secret, so a slow
// deliberately-expensive KDF buys nothing here and would slow down every authenticated
// request that uses it).
public class ApiKey : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
