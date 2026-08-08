using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Tasks;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class TimeEntriesService(IAppDbContext db) : ITimeEntriesService
{
    public async Task<TimeEntryDto?> GetActiveAsync(string companyId, string userId, CancellationToken cancellationToken = default)
    {
        var entry = await db.TimeEntries.Include(te => te.User).Include(te => te.Task)
            .SingleOrDefaultAsync(te => te.UserId == userId && te.StoppedAt == null, cancellationToken);
        return entry is null ? null : ToDto(entry);
    }

    public async Task<TimeEntryDto> StartAsync(string companyId, string userId, string taskId, StartTimeRequest request, CancellationToken cancellationToken = default)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            ?? throw new NotFoundException("Task not found");

        var alreadyRunning = await db.TimeEntries.AnyAsync(te => te.UserId == userId && te.StoppedAt == null, cancellationToken);
        if (alreadyRunning)
        {
            throw new ConflictException("A timer is already running for this user");
        }

        var entry = new TimeEntry
        {
            CompanyId = companyId,
            TaskId = taskId,
            UserId = userId,
            Note = request.Note,
        };

        db.TimeEntries.Add(entry);

        if (task.Status == TaskItemStatus.Todo)
        {
            task.Status = TaskItemStatus.InProgress;
        }

        await db.SaveChangesAsync(cancellationToken);

        entry.User = await db.Users.FindAsync([userId], cancellationToken);
        entry.Task = task;
        return ToDto(entry);
    }

    public async Task<TimeEntryDto> StopAsync(string companyId, string userId, string taskId, CancellationToken cancellationToken = default)
    {
        var entry = await db.TimeEntries.Include(te => te.User).Include(te => te.Task)
            .SingleOrDefaultAsync(te => te.TaskId == taskId && te.UserId == userId && te.StoppedAt == null, cancellationToken)
            ?? throw new NotFoundException("No running timer found for this task");

        entry.StoppedAt = DateTime.UtcNow;
        entry.DurationSec = (int)(entry.StoppedAt.Value - entry.StartedAt).TotalSeconds;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<IReadOnlyList<TimeEntryDto>> ListForTaskAsync(string companyId, string taskId, CancellationToken cancellationToken = default)
    {
        var entries = await db.TimeEntries.Include(te => te.User).Include(te => te.Task)
            .Where(te => te.TaskId == taskId)
            .OrderByDescending(te => te.StartedAt)
            .ToListAsync(cancellationToken);
        return entries.Select(ToDto).ToList();
    }

    public async Task RemoveAsync(string companyId, string taskId, string entryId, CancellationToken cancellationToken = default)
    {
        var entry = await db.TimeEntries.SingleOrDefaultAsync(te => te.Id == entryId && te.TaskId == taskId, cancellationToken)
            ?? throw new NotFoundException("Time entry not found");

        db.TimeEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TimeEntryDto ToDto(TimeEntry te) => new(
        te.Id, te.TaskId, te.Task?.Title, te.UserId, te.User?.Name, te.StartedAt, te.StoppedAt, te.DurationSec, te.Note);
}
