using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class AiQueryResult : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Question { get; set; } = string.Empty;
    public QueryIntent? Intent { get; set; }
    public AiJobStatus Status { get; set; } = AiJobStatus.Pending;
    public string? ResultRowsJson { get; set; }
    public string? Answer { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
