using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// One row per (CompanyId, MetricKey). Periodic counters reset in place when their period
// rolls over - this backs a live usage dashboard, not a historical ledger.
public class UsageCounter : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string MetricKey { get; set; } = string.Empty;
    public UsageMetricKind Kind { get; set; }
    public int CurrentValue { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
