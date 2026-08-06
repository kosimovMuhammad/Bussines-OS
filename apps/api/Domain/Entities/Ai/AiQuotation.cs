using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class AiQuotation : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string DealId { get; set; } = string.Empty;
    public Deal Deal { get; set; } = null!;
    public string ItemsJson { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public AiJobStatus Status { get; set; } = AiJobStatus.Pending;
    public string? FileAssetId { get; set; }
    public FileAsset? FileAsset { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
