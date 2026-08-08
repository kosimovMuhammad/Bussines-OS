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

public record QueryTasksRequest(TaskItemStatus? Status, string? AssignedTo, string? DealId, string? ContactId, bool IncludeArchived = false);

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
    DateTime UpdatedAt,
    bool IsArchived = false);

public record BulkUpdateTasksRequest(List<string> Ids, UpdateTaskRequest Patch);

public record TimeEntryDto(string Id, string TaskId, string? TaskTitle, string? UserId, string? UserName, DateTime StartedAt, DateTime? StoppedAt, int? DurationSec, string? Note);

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
    IReadOnlyList<TimeEntryDto> TimeEntries,
    bool IsArchived = false)
    : TaskDto(Id, CompanyId, Title, Description, Priority, Status, AssignedTo, AssigneeName, DueDate, DealId, DealTitle, ContactId, ContactName, CreatedAt, UpdatedAt, IsArchived);

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

public class BulkUpdateTasksRequestValidator : AbstractValidator<BulkUpdateTasksRequest>
{
    public BulkUpdateTasksRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(ids => ids.Count <= 200).WithMessage("A bulk operation is limited to 200 ids at a time");
        RuleFor(x => x.Patch).SetValidator(new UpdateTaskRequestValidator());
    }
}
