using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

// Runs unscoped (no ICurrentTenantContext active outside a request - see DataContext.ApplyTenantFilter),
// same as WorkflowSchedulerService's relative-trigger scan, so it naturally sweeps every company
// in one pass and dispatches per-task via task.CompanyId.
public class TaskDueNotificationService(IAppDbContext db, INotificationService notifications) : ITaskDueNotificationService
{
    private static readonly TimeSpan DueWindow = TimeSpan.FromHours(24);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await NotifyDueAsync(cancellationToken);
        await NotifyOverdueAsync(cancellationToken);
    }

    private async Task NotifyDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var horizon = now.Add(DueWindow);

        var dueTasks = await db.Tasks
            .Where(t => t.Status != TaskItemStatus.Completed && t.Status != TaskItemStatus.Archived &&
                t.AssignedTo != null && t.DueDate != null && t.DueDate <= horizon && t.LastDueNotifiedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var task in dueTasks)
        {
            task.LastDueNotifiedAt = now;
            await notifications.NotifyAsync(task.CompanyId, task.AssignedTo!, NotificationType.TaskDue,
                "Task due soon", $"\"{task.Title}\" is due soon", "TaskItem", task.Id, cancellationToken);
        }

        if (dueTasks.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task NotifyOverdueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var overdueTasks = await db.Tasks
            .Where(t => t.Status != TaskItemStatus.Completed && t.Status != TaskItemStatus.Archived &&
                t.AssignedTo != null && t.DueDate != null && t.DueDate < now && t.LastOverdueNotifiedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var task in overdueTasks)
        {
            task.LastOverdueNotifiedAt = now;
            await notifications.NotifyAsync(task.CompanyId, task.AssignedTo!, NotificationType.TaskOverdue,
                "Task overdue", $"\"{task.Title}\" is overdue", "TaskItem", task.Id, cancellationToken);
        }

        if (overdueTasks.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
