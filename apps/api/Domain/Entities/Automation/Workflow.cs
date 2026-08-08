using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class Workflow : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public WorkflowTrigger? Trigger { get; set; }
    public ICollection<WorkflowConditionGroup> ConditionGroups { get; set; } = new List<WorkflowConditionGroup>();
    public ICollection<WorkflowAction> Actions { get; set; } = new List<WorkflowAction>();
    public ICollection<WorkflowExecution> Executions { get; set; } = new List<WorkflowExecution>();
}
