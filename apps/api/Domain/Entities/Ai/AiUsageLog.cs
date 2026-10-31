using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class AiUsageLog : ITenantScoped, IImmutableRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? UserId { get; set; }
    public User? User { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public int RequestCount { get; set; } = 1;
    public int? TokensUsed { get; set; }
    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
