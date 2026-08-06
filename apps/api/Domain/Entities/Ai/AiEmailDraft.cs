using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class AiEmailDraft : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string ContactId { get; set; } = string.Empty;
    public Contact Contact { get; set; } = null!;
    public string? Context { get; set; }
    public AiJobStatus Status { get; set; } = AiJobStatus.Pending;
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
