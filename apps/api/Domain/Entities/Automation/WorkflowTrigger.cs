using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// CompanyId is carried here (not just reachable via Workflow) because the Phase 4
// scheduler polls this table directly across all tenants, outside any HTTP request's
// ambient tenant scope.
public class WorkflowTrigger : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string WorkflowId { get; set; } = string.Empty;
    public Workflow Workflow { get; set; } = null!;
    public WorkflowTriggerType Type { get; set; }
    public string? ConfigJson { get; set; }
    public string? CronExpression { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
