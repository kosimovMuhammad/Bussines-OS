using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class WorkflowExecution : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string WorkflowId { get; set; } = string.Empty;
    public Workflow Workflow { get; set; } = null!;
    public WorkflowTriggerSource TriggeredBy { get; set; }
    public string? TriggerContextJson { get; set; }
    public ExecutionStatus Status { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<WorkflowExecutionStep> Steps { get; set; } = new List<WorkflowExecutionStep>();
}
