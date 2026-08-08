using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Services;

public interface IWorkflowActionExecutor
{
    // record is the live, tracked entity that triggered the workflow (Contact/Deal/
    // TaskItem), or null for a record-less recurring schedule - UpdateField/AssignOwner
    // need a record and fail cleanly (a single Failed step, not a crash) when it's null.
    // record is otherwise mutated directly and relies on the caller's single
    // SaveChangesAsync to persist alongside the WorkflowExecution/Step rows.
    // Returns a short JSON-serializable output string for the step's Output column.
    Task<string?> ExecuteAsync(string companyId, string workflowCreatedByUserId, WorkflowAction action, object? record, IReadOnlyDictionary<string, string?> fieldValues, CancellationToken cancellationToken = default);
}
