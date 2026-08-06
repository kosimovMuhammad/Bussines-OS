using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Tasks;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class TasksService(IAppDbContext db) : ITasksService
{
    public async Task<IReadOnlyList<TaskDto>> ListAsync(string companyId, string actorUserId, Role actorRole, QueryTasksRequest query, CancellationToken cancellationToken = default)
    {
        var tasks = db.Tasks.Include(t => t.Assignee).Include(t => t.Deal).Include(t => t.Contact).AsQueryable();

        if (actorRole == Role.Employee)
        {
            tasks = tasks.Where(t => t.AssignedTo == actorUserId);
        }

        if (query.Status is not null) tasks = tasks.Where(t => t.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.AssignedTo)) tasks = tasks.Where(t => t.AssignedTo == query.AssignedTo);
        if (!string.IsNullOrWhiteSpace(query.DealId)) tasks = tasks.Where(t => t.DealId == query.DealId);
        if (!string.IsNullOrWhiteSpace(query.ContactId)) tasks = tasks.Where(t => t.ContactId == query.ContactId);

        var items = await tasks.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<TaskDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var task = await db.Tasks
            .Include(t => t.Assignee).Include(t => t.Deal).Include(t => t.Contact).Include(t => t.TimeEntries).ThenInclude(te => te.User)
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Task not found");

        var totalSec = task.TimeEntries.Sum(te => te.DurationSec ?? 0);
        return new TaskDetailDto(
            task.Id, task.CompanyId, task.Title, task.Description, task.Priority.ToString(), task.Status.ToString(),
            task.AssignedTo, task.Assignee?.Name, task.DueDate, task.DealId, task.Deal?.Title, task.ContactId,
            task.Contact is null ? null : $"{task.Contact.FirstName} {task.Contact.LastName}".Trim(),
            task.CreatedAt, task.UpdatedAt, totalSec, task.TimeEntries.Select(ToTimeEntryDto).ToList());
    }

    public async Task<TaskDto> CreateAsync(string companyId, CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        if (request.AssignedTo is not null)
        {
            await ValidateUserAsync(companyId, request.AssignedTo, cancellationToken);
        }
        if (request.DealId is not null)
        {
            await ValidateDealAsync(request.DealId, cancellationToken);
        }
        if (request.ContactId is not null)
        {
            await ValidateContactAsync(request.ContactId, cancellationToken);
        }

        var task = new TaskItem
        {
            CompanyId = companyId,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Status = request.Status,
            AssignedTo = request.AssignedTo,
            DueDate = request.DueDate,
            DealId = request.DealId,
            ContactId = request.ContactId,
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);

        return await GetDtoAsync(task.Id, cancellationToken);
    }

    public async Task<TaskDto> UpdateAsync(string companyId, string actorUserId, Role actorRole, string id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Task not found");

        if (actorRole == Role.Employee && task.AssignedTo != actorUserId)
        {
            throw new ForbiddenException("You can only edit your own tasks");
        }

        if (request.AssignedTo is not null)
        {
            await ValidateUserAsync(companyId, request.AssignedTo, cancellationToken);
            task.AssignedTo = request.AssignedTo;
        }
        if (request.DealId is not null)
        {
            await ValidateDealAsync(request.DealId, cancellationToken);
            task.DealId = request.DealId;
        }
        if (request.ContactId is not null)
        {
            await ValidateContactAsync(request.ContactId, cancellationToken);
            task.ContactId = request.ContactId;
        }

        if (request.Title is not null) task.Title = request.Title;
        if (request.Description is not null) task.Description = request.Description;
        if (request.Priority is not null) task.Priority = request.Priority.Value;
        if (request.DueDate is not null) task.DueDate = request.DueDate;

        await db.SaveChangesAsync(cancellationToken);
        return await GetDtoAsync(task.Id, cancellationToken);
    }

    public async Task<TaskDto> UpdateStatusAsync(string companyId, string id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Task not found");

        task.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return await GetDtoAsync(task.Id, cancellationToken);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Task not found");

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<TaskDto> GetDtoAsync(string id, CancellationToken cancellationToken)
    {
        var task = await db.Tasks.Include(t => t.Assignee).Include(t => t.Deal).Include(t => t.Contact)
            .SingleAsync(t => t.Id == id, cancellationToken);
        return ToDto(task);
    }

    private async Task ValidateUserAsync(string companyId, string userId, CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);
        if (!exists) throw new NotFoundException("Assignee not found in this company");
    }

    private async Task ValidateDealAsync(string dealId, CancellationToken cancellationToken)
    {
        var exists = await db.Deals.AnyAsync(d => d.Id == dealId, cancellationToken);
        if (!exists) throw new NotFoundException("Deal not found");
    }

    private async Task ValidateContactAsync(string contactId, CancellationToken cancellationToken)
    {
        var exists = await db.Contacts.AnyAsync(c => c.Id == contactId, cancellationToken);
        if (!exists) throw new NotFoundException("Contact not found");
    }

    private static TimeEntryDto ToTimeEntryDto(TimeEntry te) => new(
        te.Id, te.TaskId, te.UserId, te.User?.Name, te.StartedAt, te.StoppedAt, te.DurationSec, te.Note);

    private static TaskDto ToDto(TaskItem t) => new(
        t.Id, t.CompanyId, t.Title, t.Description, t.Priority.ToString(), t.Status.ToString(),
        t.AssignedTo, t.Assignee?.Name, t.DueDate, t.DealId, t.Deal?.Title, t.ContactId,
        t.Contact is null ? null : $"{t.Contact.FirstName} {t.Contact.LastName}".Trim(), t.CreatedAt, t.UpdatedAt);
}
