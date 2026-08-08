using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class WorkflowCondition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConditionGroupId { get; set; } = string.Empty;
    public WorkflowConditionGroup ConditionGroup { get; set; } = null!;
    public string Field { get; set; } = string.Empty;
    public ConditionOperator Operator { get; set; }
    public string? Value { get; set; }
}
