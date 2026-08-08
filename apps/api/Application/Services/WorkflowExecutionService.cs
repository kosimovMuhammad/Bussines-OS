using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class WorkflowExecutionService(IAppDbContext db, IWorkflowActionExecutor actionExecutor, IWorkflowRateLimiter rateLimiter) : IWorkflowExecutionService
{
    // Backstop for legitimate A->B->C chains, on top of the same-workflow re-entry guard
    // below - both are needed since a cycle can span more than one other workflow before
    // looping back.
    private const int MaxChainDepth = 5;

    private static readonly WorkflowTriggerType[] EventDrivenTriggerTypes =
    [
        WorkflowTriggerType.RecordCreated,
        WorkflowTriggerType.RecordUpdated,
        WorkflowTriggerType.RecordStatusChanged,
        WorkflowTriggerType.FieldValueMatches,
    ];

    public async Task HandleEntityChangedAsync(EntityChangedEvent evt, CancellationToken cancellationToken = default)
    {
        // Restores the chain carried across the queue boundary (see EntityChangedEvent's
        // doc comment) so RunWorkflowAsync's re-entry check below sees the full ancestry,
        // not just whichever workflow queued this specific event.
        using var chainScope = WorkflowExecutionContext.PushAll((IEnumerable<string>?)evt.TriggeringWorkflowIds ?? []);

        var candidates = await db.WorkflowTriggers
            .Include(t => t.Workflow)
            .Where(t => t.CompanyId == evt.CompanyId && t.Workflow.IsActive && EventDrivenTriggerTypes.Contains(t.Type))
            .ToListAsync(cancellationToken);

        foreach (var trigger in candidates)
        {
            var config = ParseTriggerConfig(trigger.ConfigJson);
            if (config.EntityType is null || !string.Equals(config.EntityType, evt.EntityType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TriggerMatchesEvent(trigger.Type, config, evt))
            {
                continue;
            }

            var record = await LoadRecordAsync(evt.EntityType, evt.EntityId, cancellationToken);
            if (record is null)
            {
                continue;
            }

            var fieldValues = FlattenEntity(record, evt.EntityType);

            if (config.Field is not null && config.Operator is not null &&
                Enum.TryParse<ConditionOperator>(config.Operator, true, out var op))
            {
                var synthetic = new WorkflowCondition { Field = config.Field, Operator = op, Value = config.Value };
                if (!ConditionTreeEvaluator.EvaluateCondition(synthetic, fieldValues))
                {
                    continue;
                }
            }

            await RunWorkflowAsync(trigger.Workflow, evt.EntityType, evt.EntityId, record, WorkflowTriggerSource.System, cancellationToken);
        }
    }

    // Shared by both the event-driven path above and the Phase 4 scheduler: evaluates this
    // workflow's own condition tree against the record (if any) and runs its actions,
    // continue-and-log on failure, recording one WorkflowExecution + one WorkflowExecutionStep
    // per action regardless of outcome. record/entityType/entityId are null for a
    // record-less recurring schedule (e.g. "every day at 9am") - actions that need a live
    // record (UpdateField/AssignOwner) simply fail that one step in that case.
    public async Task RunWorkflowAsync(Workflow workflow, string? entityType, string? entityId, object? record, WorkflowTriggerSource source, CancellationToken cancellationToken = default)
    {
        if (WorkflowExecutionContext.Current.Contains(workflow.Id))
        {
            await RecordSkippedExecutionAsync(workflow, entityType, entityId, source,
                "Circular trigger chain detected - this workflow is already executing within the current chain", cancellationToken);
            return;
        }

        if (WorkflowExecutionContext.Current.Count >= MaxChainDepth)
        {
            await RecordSkippedExecutionAsync(workflow, entityType, entityId, source,
                $"Trigger chain exceeded the maximum depth ({MaxChainDepth})", cancellationToken);
            return;
        }

        if (!rateLimiter.TryAcquire(workflow.CompanyId))
        {
            await RecordSkippedExecutionAsync(workflow, entityType, entityId, source,
                "Company execution rate limit exceeded for the current window", cancellationToken);
            return;
        }

        using var chainScope = WorkflowExecutionContext.Push(workflow.Id);

        var fieldValues = record is not null && entityType is not null
            ? FlattenEntity(record, entityType)
            : new Dictionary<string, string?>();

        var conditionGroups = await db.WorkflowConditionGroups
            .Include(g => g.Conditions)
            .Where(g => g.WorkflowId == workflow.Id)
            .ToListAsync(cancellationToken);

        if (!ConditionTreeEvaluator.Evaluate(conditionGroups, fieldValues))
        {
            return;
        }

        var actions = await db.WorkflowActions
            .Where(a => a.WorkflowId == workflow.Id)
            .OrderBy(a => a.OrderIndex)
            .ToListAsync(cancellationToken);

        var execution = new WorkflowExecution
        {
            CompanyId = workflow.CompanyId,
            WorkflowId = workflow.Id,
            TriggeredBy = source,
            TriggerContextJson = JsonSerializer.Serialize(new { entityType, entityId }),
            StartedAt = DateTime.UtcNow,
        };
        db.WorkflowExecutions.Add(execution);

        var anyFailed = false;
        var anySucceeded = false;

        // Continue-and-log: one bad action must not silently block the rest of the chain.
        foreach (var action in actions)
        {
            var step = new WorkflowExecutionStep
            {
                WorkflowExecutionId = execution.Id,
                ActionId = action.Id,
                ActionType = action.Type,
                ExecutedAt = DateTime.UtcNow,
            };

            try
            {
                step.Output = await actionExecutor.ExecuteAsync(workflow.CompanyId, workflow.CreatedBy, action, record, fieldValues, cancellationToken);
                step.Status = StepStatus.Success;
                anySucceeded = true;
            }
            catch (Exception ex)
            {
                step.Status = StepStatus.Failed;
                step.Error = JsonSerializer.Serialize(new { message = ex.Message });
                anyFailed = true;
            }

            execution.Steps.Add(step);
        }

        execution.Status = (anyFailed, anySucceeded) switch
        {
            (true, true) => ExecutionStatus.PartialFailure,
            (true, false) => ExecutionStatus.Failed,
            _ => ExecutionStatus.Success,
        };
        execution.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordSkippedExecutionAsync(Workflow workflow, string? entityType, string? entityId, WorkflowTriggerSource source, string reason, CancellationToken cancellationToken)
    {
        db.WorkflowExecutions.Add(new WorkflowExecution
        {
            CompanyId = workflow.CompanyId,
            WorkflowId = workflow.Id,
            TriggeredBy = source,
            TriggerContextJson = JsonSerializer.Serialize(new { entityType, entityId, skippedReason = reason }),
            Status = ExecutionStatus.Skipped,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool TriggerMatchesEvent(WorkflowTriggerType type, TriggerConfig config, EntityChangedEvent evt) => type switch
    {
        WorkflowTriggerType.RecordCreated => evt.Kind == EntityChangeKind.Created,

        WorkflowTriggerType.RecordUpdated => evt.Kind == EntityChangeKind.Updated &&
            (config.Field is null || evt.Changes.Any(c => string.Equals(c.PropertyName, config.Field, StringComparison.OrdinalIgnoreCase))),

        WorkflowTriggerType.RecordStatusChanged => evt.Kind == EntityChangeKind.Updated && config.Field is not null &&
            MatchesStatusChange(config, evt),

        WorkflowTriggerType.FieldValueMatches => evt.Kind != EntityChangeKind.Deleted,

        _ => false,
    };

    private static bool MatchesStatusChange(TriggerConfig config, EntityChangedEvent evt)
    {
        var change = evt.Changes.SingleOrDefault(c => string.Equals(c.PropertyName, config.Field, StringComparison.OrdinalIgnoreCase));
        if (change is null)
        {
            return false;
        }

        if (config.FromStatus is not null && !string.Equals(change.Before?.ToString(), config.FromStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (config.ToStatus is not null && !string.Equals(change.After?.ToString(), config.ToStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private async Task<object?> LoadRecordAsync(string entityType, string entityId, CancellationToken cancellationToken) => entityType switch
    {
        nameof(Contact) => await db.Contacts.FindAsync([entityId], cancellationToken),
        nameof(Deal) => await db.Deals.FindAsync([entityId], cancellationToken),
        nameof(TaskItem) => await db.Tasks.FindAsync([entityId], cancellationToken),
        _ => null,
    };

    // Flattens scalar properties of a tracked entity into a case-insensitive lookup usable
    // both for condition evaluation (bare field name, e.g. "Stage") and template resolution
    // (dotted, e.g. "deal.stage").
    private static Dictionary<string, string?> FlattenEntity(object record, string entityType)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var prefix = entityType.ToLowerInvariant();

        foreach (var property in record.GetType().GetProperties())
        {
            if (!IsScalarType(property.PropertyType))
            {
                continue;
            }

            var value = property.GetValue(record)?.ToString();
            values[property.Name] = value;
            values[$"{prefix}.{property.Name}"] = value;
        }

        return values;
    }

    private static bool IsScalarType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string) ||
            underlying == typeof(decimal) || underlying == typeof(DateTime) || underlying == typeof(Guid);
    }

    private static TriggerConfig ParseTriggerConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new TriggerConfig(null, null, null, null, null, null);
        }

        try
        {
            return JsonSerializer.Deserialize<TriggerConfig>(configJson, JsonOptions) ?? new TriggerConfig(null, null, null, null, null, null);
        }
        catch (JsonException)
        {
            return new TriggerConfig(null, null, null, null, null, null);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record TriggerConfig(
        [property: JsonPropertyName("entityType")] string? EntityType,
        [property: JsonPropertyName("field")] string? Field,
        [property: JsonPropertyName("fromStatus")] string? FromStatus,
        [property: JsonPropertyName("toStatus")] string? ToStatus,
        [property: JsonPropertyName("operator")] string? Operator,
        [property: JsonPropertyName("value")] string? Value);
}
