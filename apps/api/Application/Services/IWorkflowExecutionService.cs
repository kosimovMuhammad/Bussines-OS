using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface IWorkflowExecutionService
{
    // Entry point called from the queued work item ActivityLogSaveChangesInterceptor
    // dispatches after a save commits. Matches active workflows' event-driven triggers
    // (RecordCreated/RecordUpdated/RecordStatusChanged/FieldValueMatches - TimeBased is
    // handled separately by the Phase 4 scheduler) against the event and runs any that match.
    Task HandleEntityChangedAsync(EntityChangedEvent evt, CancellationToken cancellationToken = default);

    // Shared execution core, also called directly by IWorkflowSchedulerService for
    // TimeBased triggers (record/entityType/entityId are null for a record-less
    // recurring schedule).
    Task RunWorkflowAsync(Workflow workflow, string? entityType, string? entityId, object? record, WorkflowTriggerSource source, CancellationToken cancellationToken = default);
}
