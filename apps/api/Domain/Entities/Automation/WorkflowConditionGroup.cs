using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

// Not ITenantScoped - always loaded via its parent Workflow, never queried standalone
// by company (same reasoning as WorkflowCondition below).
public class WorkflowConditionGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public Workflow Workflow { get; set; } = null!;
    public string? ParentGroupId { get; set; }
    public WorkflowConditionGroup? ParentGroup { get; set; }
    public ConditionLogicOperator LogicOperator { get; set; }

    public ICollection<WorkflowConditionGroup> ChildGroups { get; set; } = new List<WorkflowConditionGroup>();
    public ICollection<WorkflowCondition> Conditions { get; set; } = new List<WorkflowCondition>();
}
