using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class WorkflowExecutionStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowExecutionId { get; set; } = string.Empty;
    public WorkflowExecution WorkflowExecution { get; set; } = null!;
    public string? ActionId { get; set; }
    public WorkflowAction? Action { get; set; }
    public WorkflowActionType ActionType { get; set; }
    public StepStatus Status { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
