using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Cronos;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class WorkflowSchedulerService(IAppDbContext db, IWorkflowExecutionService executionService) : IWorkflowSchedulerService
{
    public async Task RunDueTriggersAsync(CancellationToken cancellationToken = default)
    {
        await RunRecurringTriggersAsync(cancellationToken);
        await RunRelativeTaskDueDateTriggersAsync(cancellationToken);
    }

    // Recurring (CronExpression + NextRunAtUtc set). Every firing re-checks Workflow.IsActive
    // - a workflow could have been deactivated between when this row became due and now.
    private async Task RunRecurringTriggersAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var due = await db.WorkflowTriggers
            .Include(t => t.Workflow)
            .Where(t => t.Type == WorkflowTriggerType.TimeBased && t.CronExpression != null &&
                t.NextRunAtUtc != null && t.NextRunAtUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var trigger in due)
        {
            trigger.NextRunAtUtc = ComputeNextRun(trigger.CronExpression!, now);

            if (!trigger.Workflow.IsActive)
            {
                continue;
            }

            await executionService.RunWorkflowAsync(trigger.Workflow, null, null, null, WorkflowTriggerSource.Schedule, cancellationToken);
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // Relative triggers ("N days after Task.DueDate") - MVP scope covers TaskItem.DueDate
    // only (the spec's own example), not a generic multi-entity date-field scanner.
    // Idempotency: a relative trigger fires at most once per record ever, checked by
    // scanning this workflow's past executions for the record id in TriggerContextJson -
    // simple and correct for this volume, not a dedicated fire-log table.
    private async Task RunRelativeTaskDueDateTriggersAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var relativeTriggers = await db.WorkflowTriggers
            .Include(t => t.Workflow)
            .Where(t => t.Type == WorkflowTriggerType.TimeBased && t.CronExpression == null && t.Workflow.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var trigger in relativeTriggers)
        {
            var config = ParseRelativeConfig(trigger.ConfigJson);
            if (config?.EntityType is null || !string.Equals(config.EntityType, nameof(TaskItem), StringComparison.OrdinalIgnoreCase) ||
                config.DateField is null || !string.Equals(config.DateField, nameof(TaskItem.DueDate), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dueTasks = await db.Tasks
                .Where(t => t.CompanyId == trigger.CompanyId && t.DueDate != null)
                .ToListAsync(cancellationToken);

            foreach (var task in dueTasks.Where(t => t.DueDate!.Value.AddDays(config.OffsetDays) <= now))
            {
                var alreadyFired = await db.WorkflowExecutions.AnyAsync(e =>
                    e.WorkflowId == trigger.WorkflowId && e.TriggerContextJson != null && e.TriggerContextJson.Contains(task.Id),
                    cancellationToken);

                if (alreadyFired)
                {
                    continue;
                }

                await executionService.RunWorkflowAsync(trigger.Workflow, nameof(TaskItem), task.Id, task, WorkflowTriggerSource.Schedule, cancellationToken);
            }
        }
    }

    private static DateTime? ComputeNextRun(string cronExpression, DateTime fromUtc)
    {
        try
        {
            return CronExpression.Parse(cronExpression).GetNextOccurrence(fromUtc, TimeZoneInfo.Utc);
        }
        catch (CronFormatException)
        {
            return null;
        }
    }

    private static RelativeTriggerConfig? ParseRelativeConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RelativeTriggerConfig>(configJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record RelativeTriggerConfig(
        [property: JsonPropertyName("entityType")] string? EntityType,
        [property: JsonPropertyName("dateField")] string? DateField,
        [property: JsonPropertyName("offsetDays")] int OffsetDays);
}
