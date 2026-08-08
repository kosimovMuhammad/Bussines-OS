using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class WorkflowAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public Workflow Workflow { get; set; } = null!;
    public WorkflowActionType Type { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkflowExecutionStep> ExecutionSteps { get; set; } = new List<WorkflowExecutionStep>();
}
