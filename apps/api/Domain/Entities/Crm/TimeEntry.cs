using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class TimeEntry : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string TaskId { get; set; } = string.Empty;
    public TaskItem Task { get; set; } = null!;
    public string? UserId { get; set; }
    public User? User { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StoppedAt { get; set; }
    public int? DurationSec { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
