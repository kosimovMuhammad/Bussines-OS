using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Saved/custom report config. EntityType/GroupBy/field names inside FiltersJson and
// MetricsJson are validated server-side against a whitelist at run time
// (ReportQueryService) - this row only stores what the user configured, never anything
// executable. No raw SQL or client-controlled joins are possible from this data.
public class Report : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? FiltersJson { get; set; }
    public string? GroupBy { get; set; }
    public string MetricsJson { get; set; } = "[]";
    public string? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
