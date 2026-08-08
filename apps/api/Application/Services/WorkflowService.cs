using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Automation;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Cronos;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class WorkflowService(IAppDbContext db, ILimitEnforcementService limitEnforcement) : IWorkflowService
{
    private const int MaxActionsPerWorkflow = 10;
    private const int MaxConditionGroupDepth = 2;

    public async Task<IReadOnlyList<WorkflowDto>> ListAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var workflows = await db.Workflows.OrderByDescending(w => w.CreatedAt).ToListAsync(cancellationToken);
        return workflows.Select(ToDto).ToList();
    }

    public async Task<WorkflowDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var workflow = await LoadDetailAsync(id, cancellationToken)
            ?? throw new NotFoundException("Workflow not found");
        return ToDetailDto(workflow);
    }

    public async Task<WorkflowDetailDto> CreateAsync(string companyId, string actorUserId, CreateWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        await limitEnforcement.CheckLimitAsync(companyId, UsageMetricKeys.AutomationWorkflows, cancellationToken);

        ValidateActionCount(request.Actions.Count);
        ValidateConditionDepth(request.ConditionGroups);

        var workflow = new Workflow
        {
            CompanyId = companyId,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedBy = actorUserId,
        };

        workflow.Trigger = BuildTrigger(companyId, workflow.Id, request.Trigger);
        workflow.ConditionGroups = BuildGroups(workflow.Id, null, request.ConditionGroups ?? []);
        workflow.Actions = BuildActions(workflow.Id, request.Actions);

        db.Workflows.Add(workflow);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(companyId, workflow.Id, cancellationToken);
    }

    public async Task<WorkflowDetailDto> UpdateAsync(string companyId, string id, UpdateWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var workflow = await LoadDetailAsync(id, cancellationToken)
            ?? throw new NotFoundException("Workflow not found");

        if (request.Name is not null)
        {
            workflow.Name = request.Name;
        }

        if (request.Description is not null)
        {
            workflow.Description = request.Description;
        }

        if (request.IsActive is not null)
        {
            workflow.IsActive = request.IsActive.Value;
        }

        if (request.Actions is not null)
        {
            ValidateActionCount(request.Actions.Count);
            db.WorkflowActions.RemoveRange(workflow.Actions);
            workflow.Actions = BuildActions(id, request.Actions);
        }

        if (request.ConditionGroups is not null)
        {
            ValidateConditionDepth(request.ConditionGroups);
            db.WorkflowConditionGroups.RemoveRange(workflow.ConditionGroups);
            workflow.ConditionGroups = BuildGroups(id, null, request.ConditionGroups);
        }

        if (request.Trigger is not null)
        {
            if (workflow.Trigger is not null)
            {
                db.WorkflowTriggers.Remove(workflow.Trigger);
            }

            workflow.Trigger = BuildTrigger(companyId, id, request.Trigger);
        }

        workflow.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(companyId, id, cancellationToken);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var workflow = await db.Workflows.SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Workflow not found");

        db.Workflows.Remove(workflow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowDetailDto> ToggleAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var workflow = await db.Workflows.SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Workflow not found");

        workflow.IsActive = !workflow.IsActive;
        workflow.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(companyId, id, cancellationToken);
    }

    public async Task<PagedResult<WorkflowExecutionDto>> ListExecutionsAsync(string companyId, string workflowId, QueryWorkflowExecutionsRequest query, CancellationToken cancellationToken = default)
    {
        var workflowExists = await db.Workflows.AnyAsync(w => w.Id == workflowId, cancellationToken);
        if (!workflowExists)
        {
            throw new NotFoundException("Workflow not found");
        }

        var source = db.WorkflowExecutions.Where(e => e.WorkflowId == workflowId);
        var total = await source.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await source
            .OrderByDescending(e => e.StartedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<WorkflowExecutionDto>(items.Select(ToExecutionDto).ToList(), page, limit, total);
    }

    public async Task<WorkflowExecutionDetailDto> GetExecutionAsync(string companyId, string workflowId, string executionId, CancellationToken cancellationToken = default)
    {
        var execution = await db.WorkflowExecutions
            .Include(e => e.Steps)
            .SingleOrDefaultAsync(e => e.Id == executionId && e.WorkflowId == workflowId, cancellationToken)
            ?? throw new NotFoundException("Execution not found");

        return ToExecutionDetailDto(execution);
    }

    private async Task<Workflow?> LoadDetailAsync(string id, CancellationToken cancellationToken) =>
        await db.Workflows
            .Include(w => w.Trigger)
            .Include(w => w.ConditionGroups).ThenInclude(g => g.Conditions)
            .Include(w => w.Actions)
            .SingleOrDefaultAsync(w => w.Id == id, cancellationToken);

    private static WorkflowTrigger BuildTrigger(string companyId, string workflowId, CreateWorkflowTriggerRequest request) => new()
    {
        CompanyId = companyId,
        WorkflowId = workflowId,
        Type = request.Type,
        ConfigJson = request.ConfigJson,
        CronExpression = request.CronExpression,
        NextRunAtUtc = request.Type == WorkflowTriggerType.TimeBased && request.CronExpression is not null
            ? ComputeNextRun(request.CronExpression)
            : null,
    };

    // Fails fast at create/update time rather than silently never firing - an invalid
    // cron expression is a configuration error the caller should see immediately.
    private static DateTime ComputeNextRun(string cronExpression)
    {
        try
        {
            var parsed = CronExpression.Parse(cronExpression);
            return parsed.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc)
                ?? throw new ConflictException("Cron expression has no future occurrences");
        }
        catch (CronFormatException ex)
        {
            throw new ConflictException($"Invalid cron expression: {ex.Message}");
        }
    }

    private static List<WorkflowAction> BuildActions(string workflowId, IReadOnlyList<CreateWorkflowActionRequest> requests) =>
        requests.Select(a => new WorkflowAction
        {
            WorkflowId = workflowId,
            Type = a.Type,
            ConfigJson = a.ConfigJson,
            OrderIndex = a.OrderIndex,
        }).ToList();

    // Returns a FLAT list (both root and nested groups) so every group is directly
    // reachable via Workflow.ConditionGroups - the exact navigation EF's FK is configured
    // on - rather than relying on transitive discovery through nested ChildGroups.
    private static List<WorkflowConditionGroup> BuildGroups(string workflowId, string? parentGroupId, IReadOnlyList<CreateConditionGroupRequest> requests)
    {
        var result = new List<WorkflowConditionGroup>();

        foreach (var request in requests)
        {
            var group = new WorkflowConditionGroup
            {
                WorkflowId = workflowId,
                ParentGroupId = parentGroupId,
                LogicOperator = request.LogicOperator,
                Conditions = request.Conditions.Select(c => new WorkflowCondition
                {
                    Field = c.Field,
                    Operator = c.Operator,
                    Value = c.Value,
                }).ToList(),
            };

            result.Add(group);

            if (request.ChildGroups is { Count: > 0 })
            {
                result.AddRange(BuildGroups(workflowId, group.Id, request.ChildGroups));
            }
        }

        return result;
    }

    private static void ValidateActionCount(int count)
    {
        if (count > MaxActionsPerWorkflow)
        {
            throw new ConflictException($"A workflow can have at most {MaxActionsPerWorkflow} actions");
        }
    }

    private static void ValidateConditionDepth(IReadOnlyList<CreateConditionGroupRequest>? groups, int depth = 1)
    {
        if (groups is null)
        {
            return;
        }

        if (depth > MaxConditionGroupDepth)
        {
            throw new ConflictException($"Condition groups cannot be nested more than {MaxConditionGroupDepth} levels deep");
        }

        foreach (var group in groups)
        {
            ValidateConditionDepth(group.ChildGroups, depth + 1);
        }
    }

    private static WorkflowDto ToDto(Workflow w) => new(w.Id, w.Name, w.Description, w.IsActive, w.CreatedBy, w.CreatedAt, w.UpdatedAt);

    private static WorkflowDetailDto ToDetailDto(Workflow w)
    {
        var byParent = w.ConditionGroups.ToLookup(g => g.ParentGroupId);

        ConditionGroupDto BuildGroupDto(WorkflowConditionGroup g) => new(
            g.Id, g.LogicOperator,
            g.Conditions.Select(c => new ConditionDto(c.Id, c.Field, c.Operator, c.Value)).ToList(),
            byParent[g.Id].Select(BuildGroupDto).ToList());

        var rootGroups = byParent[null].Select(BuildGroupDto).ToList();

        return new WorkflowDetailDto(
            w.Id, w.Name, w.Description, w.IsActive, w.CreatedBy, w.CreatedAt, w.UpdatedAt,
            w.Trigger is null ? null : new WorkflowTriggerDto(w.Trigger.Id, w.Trigger.Type, w.Trigger.ConfigJson, w.Trigger.CronExpression, w.Trigger.NextRunAtUtc),
            rootGroups,
            w.Actions.OrderBy(a => a.OrderIndex).Select(a => new WorkflowActionDto(a.Id, a.Type, a.ConfigJson, a.OrderIndex)).ToList());
    }

    private static WorkflowExecutionDto ToExecutionDto(WorkflowExecution e) => new(
        e.Id, e.WorkflowId, e.TriggeredBy, e.Status, e.StartedAt, e.CompletedAt);

    private static WorkflowExecutionDetailDto ToExecutionDetailDto(WorkflowExecution e) => new(
        e.Id, e.WorkflowId, e.TriggeredBy, e.TriggerContextJson, e.Status, e.StartedAt, e.CompletedAt,
        e.Steps.OrderBy(s => s.ExecutedAt).Select(s => new WorkflowExecutionStepDto(s.Id, s.ActionId, s.ActionType, s.Status, s.Output, s.Error, s.ExecutedAt)).ToList());
}
