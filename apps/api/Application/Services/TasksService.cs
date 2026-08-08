using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Activity;
using BusinessOS.Application.ViewModels.Tasks;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class TasksService(IAppDbContext db, INotificationService notifications, IActivityLogService activityLog) : ITasksService
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
        if (!query.IncludeArchived) tasks = tasks.Where(t => !t.IsArchived);

        var items = await tasks.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(string companyId, string id, CancellationToken cancellationToken = default) =>
        activityLog.GetTaskTimelineAsync(companyId, id, cancellationToken);

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
            task.CreatedAt, task.UpdatedAt, totalSec, task.TimeEntries.Select(te => ToTimeEntryDto(te, task.Title)).ToList());
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

        if (task.AssignedTo is not null)
        {
            await notifications.NotifyAsync(companyId, task.AssignedTo, NotificationType.TaskAssigned,
                "Task assigned to you", $"\"{task.Title}\" was assigned to you", "Task", task.Id, cancellationToken);
        }

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

        var previousAssignee = task.AssignedTo;
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

        if (request.AssignedTo is not null && request.AssignedTo != previousAssignee)
        {
            await notifications.NotifyAsync(companyId, request.AssignedTo, NotificationType.TaskAssigned,
                "Task assigned to you", $"\"{task.Title}\" was assigned to you", "Task", task.Id, cancellationToken);
        }

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

    private static TimeEntryDto ToTimeEntryDto(TimeEntry te, string taskTitle) => new(
        te.Id, te.TaskId, taskTitle, te.UserId, te.User?.Name, te.StartedAt, te.StoppedAt, te.DurationSec, te.Note);

    private static TaskDto ToDto(TaskItem t) => new(
        t.Id, t.CompanyId, t.Title, t.Description, t.Priority.ToString(), t.Status.ToString(),
        t.AssignedTo, t.Assignee?.Name, t.DueDate, t.DealId, t.Deal?.Title, t.ContactId,
        t.Contact is null ? null : $"{t.Contact.FirstName} {t.Contact.LastName}".Trim(), t.CreatedAt, t.UpdatedAt, t.IsArchived);

    public async Task<BulkOperationResult> BulkUpdateAsync(string companyId, string actorUserId, Role actorRole, IReadOnlyList<string> ids, UpdateTaskRequest patch, CancellationToken cancellationToken = default)
    {
        var results = new List<BulkOperationResultItem>();
        foreach (var id in ids)
        {
            try
            {
                await UpdateAsync(companyId, actorUserId, actorRole, id, patch, cancellationToken);
                results.Add(new BulkOperationResultItem(id, true, null));
            }
            catch (Exception ex) when (ex is NotFoundException or ConflictException or ValidationAppException or ForbiddenException)
            {
                results.Add(new BulkOperationResultItem(id, false, ex.Message));
            }
        }

        return BulkOperationResult.From(results);
    }

    public async Task<BulkOperationResult> BulkDeleteAsync(string companyId, IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        var results = new List<BulkOperationResultItem>();
        foreach (var id in ids)
        {
            try
            {
                await RemoveAsync(companyId, id, cancellationToken);
                results.Add(new BulkOperationResultItem(id, true, null));
            }
            catch (Exception ex) when (ex is NotFoundException or ConflictException or ValidationAppException or ForbiddenException)
            {
                results.Add(new BulkOperationResultItem(id, false, ex.Message));
            }
        }

        return BulkOperationResult.From(results);
    }

    public async Task<string> ExportCsvAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var tasks = await db.Tasks.Where(t => t.CompanyId == companyId).OrderBy(t => t.CreatedAt).ToListAsync(cancellationToken);

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Title,Description,Priority,Status,DueDate");
        foreach (var t in tasks)
        {
            csv.AppendLine(string.Join(',', CsvUtils.Field(t.Title), CsvUtils.Field(t.Description), CsvUtils.Field(t.Priority.ToString()), CsvUtils.Field(t.Status.ToString()), CsvUtils.Field(t.DueDate?.ToString("O"))));
        }

        return csv.ToString();
    }

    // Dedup key is exact Title match - Tasks have no natural business key either (same
    // reasoning as Deals' (ContactId, Title) choice, simplified to just Title since a task
    // isn't scoped to one contact the way a deal import row is).
    public async Task<ImportResultDto> ImportCsvAsync(string companyId, string csvContent, CancellationToken cancellationToken = default)
    {
        var rows = CsvUtils.ParseRows(csvContent);
        if (rows.Count == 0)
        {
            return new ImportResultDto(0, 0, []);
        }

        var existingTitles = (await db.Tasks.Where(t => t.CompanyId == companyId).Select(t => t.Title).ToListAsync(cancellationToken)).ToHashSet();

        var imported = 0;
        var skipped = 0;
        var errors = new List<ImportErrorDto>();

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 1;

            if (row.Length == 0 || string.IsNullOrWhiteSpace(row[0]))
            {
                errors.Add(new ImportErrorDto(rowNumber, "Title is required"));
                continue;
            }

            var title = row[0];
            if (existingTitles.Contains(title))
            {
                skipped++;
                continue;
            }

            var priority = row.Length > 2 && Enum.TryParse<TaskPriority>(row[2], true, out var parsedPriority) ? parsedPriority : TaskPriority.Medium;
            var status = row.Length > 3 && Enum.TryParse<TaskItemStatus>(row[3], true, out var parsedStatus) ? parsedStatus : TaskItemStatus.Todo;
            DateTime? dueDate = row.Length > 4 && DateTime.TryParse(row[4], null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDue)
                ? DateTime.SpecifyKind(parsedDue, DateTimeKind.Utc)
                : null;

            db.Tasks.Add(new TaskItem
            {
                CompanyId = companyId,
                Title = title,
                Description = row.Length > 1 ? EmptyToNull(row[1]) : null,
                Priority = priority,
                Status = status,
                DueDate = dueDate,
            });

            existingTitles.Add(title);
            imported++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportResultDto(imported, skipped, errors);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
