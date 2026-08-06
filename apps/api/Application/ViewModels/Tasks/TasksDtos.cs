using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Tasks;

public record CreateTaskRequest(
    string Title,
    string? Description,
    TaskPriority Priority = TaskPriority.Medium,
    TaskItemStatus Status = TaskItemStatus.Todo,
    string? AssignedTo = null,
    DateTime? DueDate = null,
    string? DealId = null,
    string? ContactId = null);

public record UpdateTaskRequest(
    string? Title,
    string? Description,
    TaskPriority? Priority,
    string? AssignedTo,
    DateTime? DueDate,
    string? DealId,
    string? ContactId);

public record UpdateTaskStatusRequest(TaskItemStatus Status);

public record QueryTasksRequest(TaskItemStatus? Status, string? AssignedTo, string? DealId, string? ContactId);

public record StartTimeRequest(string? Note);

public record TaskDto(
    string Id,
    string CompanyId,
    string Title,
    string? Description,
    string Priority,
    string Status,
    string? AssignedTo,
    string? AssigneeName,
    DateTime? DueDate,
    string? DealId,
    string? DealTitle,
    string? ContactId,
    string? ContactName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record TimeEntryDto(string Id, string TaskId, string? UserId, string? UserName, DateTime StartedAt, DateTime? StoppedAt, int? DurationSec, string? Note);

public record TaskDetailDto(
    string Id,
    string CompanyId,
    string Title,
    string? Description,
    string Priority,
    string Status,
    string? AssignedTo,
    string? AssigneeName,
    DateTime? DueDate,
    string? DealId,
    string? DealTitle,
    string? ContactId,
    string? ContactName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int TotalTimeSec,
    IReadOnlyList<TimeEntryDto> TimeEntries)
    : TaskDto(Id, CompanyId, Title, Description, Priority, Status, AssignedTo, AssigneeName, DueDate, DealId, DealTitle, ContactId, ContactName, CreatedAt, UpdatedAt);

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().When(x => x.Title is not null);
    }
}

public class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
