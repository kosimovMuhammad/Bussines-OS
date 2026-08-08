using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Automation;

public record WorkflowDto(string Id, string Name, string? Description, bool IsActive, string CreatedBy, DateTime CreatedAt, DateTime UpdatedAt);

public record WorkflowTriggerDto(string Id, WorkflowTriggerType Type, string? ConfigJson, string? CronExpression, DateTime? NextRunAtUtc);

public record ConditionDto(string Id, string Field, ConditionOperator Operator, string? Value);

public record ConditionGroupDto(string Id, ConditionLogicOperator LogicOperator, IReadOnlyList<ConditionDto> Conditions, IReadOnlyList<ConditionGroupDto> ChildGroups);

public record WorkflowActionDto(string Id, WorkflowActionType Type, string ConfigJson, int OrderIndex);

public record WorkflowDetailDto(
    string Id,
    string Name,
    string? Description,
    bool IsActive,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    WorkflowTriggerDto? Trigger,
    IReadOnlyList<ConditionGroupDto> ConditionGroups,
    IReadOnlyList<WorkflowActionDto> Actions);

public record CreateWorkflowTriggerRequest(WorkflowTriggerType Type, string? ConfigJson, string? CronExpression);

public record CreateConditionRequest(string Field, ConditionOperator Operator, string? Value);

public record CreateConditionGroupRequest(
    ConditionLogicOperator LogicOperator,
    IReadOnlyList<CreateConditionRequest> Conditions,
    IReadOnlyList<CreateConditionGroupRequest>? ChildGroups);

public record CreateWorkflowActionRequest(WorkflowActionType Type, string ConfigJson, int OrderIndex);

public record CreateWorkflowRequest(
    string Name,
    string? Description,
    bool IsActive,
    CreateWorkflowTriggerRequest Trigger,
    IReadOnlyList<CreateConditionGroupRequest>? ConditionGroups,
    IReadOnlyList<CreateWorkflowActionRequest> Actions);

public record UpdateWorkflowRequest(
    string? Name,
    string? Description,
    bool? IsActive,
    CreateWorkflowTriggerRequest? Trigger,
    IReadOnlyList<CreateConditionGroupRequest>? ConditionGroups,
    IReadOnlyList<CreateWorkflowActionRequest>? Actions);

public record WorkflowExecutionDto(string Id, string WorkflowId, WorkflowTriggerSource TriggeredBy, ExecutionStatus Status, DateTime StartedAt, DateTime? CompletedAt);

public record WorkflowExecutionStepDto(string Id, string? ActionId, WorkflowActionType ActionType, StepStatus Status, string? Output, string? Error, DateTime ExecutedAt);

public record WorkflowExecutionDetailDto(
    string Id,
    string WorkflowId,
    WorkflowTriggerSource TriggeredBy,
    string? TriggerContextJson,
    ExecutionStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<WorkflowExecutionStepDto> Steps);

public record QueryWorkflowExecutionsRequest(int Page = 1, int Limit = 20);

public class CreateWorkflowRequestValidator : AbstractValidator<CreateWorkflowRequest>
{
    public CreateWorkflowRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Trigger).NotNull();
        RuleFor(x => x.Actions).NotNull();
        RuleFor(x => x.Actions.Count).LessThanOrEqualTo(10).WithMessage("A workflow can have at most 10 actions");
    }
}

public class UpdateWorkflowRequestValidator : AbstractValidator<UpdateWorkflowRequest>
{
    public UpdateWorkflowRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Actions!.Count).LessThanOrEqualTo(10).WithMessage("A workflow can have at most 10 actions").When(x => x.Actions is not null);
    }
}
