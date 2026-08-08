namespace BusinessOS.Application.Abstraction;

public enum EntityChangeKind
{
    Created,
    Updated,
    Deleted,
}

public record EntityFieldChange(string PropertyName, object? Before, object? After);

// Published by ActivityLogSaveChangesInterceptor after a successful save (SavedChangesAsync,
// not SavingChangesAsync - triggers must never fire for a change that then rolls back).
// This is the single shared change-capture mechanism: Activity Log and the Automation
// Engine's trigger matching both consume it, rather than each maintaining its own
// ChangeTracker diffing logic.
// TriggeringWorkflowIds carries the loop-guard chain across the IBackgroundTaskQueue
// boundary (AsyncLocal doesn't survive a dequeue-and-invoke cycle - see
// WorkflowExecutionContext) so a workflow action that mutates a record can't cause its
// own trigger, or an ancestor's, to re-fire.
public record EntityChangedEvent(
    string CompanyId,
    string EntityType,
    string EntityId,
    EntityChangeKind Kind,
    IReadOnlyList<EntityFieldChange> Changes,
    IReadOnlySet<string>? TriggeringWorkflowIds = null);
