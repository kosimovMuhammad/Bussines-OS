using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class CalendarSyncToken : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public string ResourceId { get; set; } = string.Empty;
    public string? SyncToken { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelToken { get; set; } = string.Empty;
    public DateTime ChannelExpiration { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
