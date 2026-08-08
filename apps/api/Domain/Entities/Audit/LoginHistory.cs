using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Not ITenantScoped: a failed login against an unknown email has no company to
// scope to. CompanyId/UserId are set when the attempt resolves to a real account.
public class LoginHistory : IImmutableRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public User? User { get; set; }
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceInfo { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public bool IsNewDevice { get; set; }
}
