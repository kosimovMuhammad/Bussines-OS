using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Activity;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class ActivityLogService(IAppDbContext db) : IActivityLogService
{
    public async Task<PagedResult<ActivityLogEntryDto>> ListAsync(string companyId, QueryActivityLogRequest query, CancellationToken cancellationToken = default) =>
        await PageAsync(db.ActivityLogs.Include(a => a.User).AsQueryable(), query, cancellationToken);

    public async Task<PagedResult<ActivityLogEntryDto>> GetForUserAsync(string companyId, string userId, QueryActivityLogRequest query, CancellationToken cancellationToken = default) =>
        await PageAsync(db.ActivityLogs.Include(a => a.User).Where(a => a.UserId == userId), query, cancellationToken);

    public async Task<PagedResult<ActivityLogEntryDto>> GetForEntityAsync(string companyId, string entityType, string entityId, QueryActivityLogRequest query, CancellationToken cancellationToken = default) =>
        await PageAsync(db.ActivityLogs.Include(a => a.User).Where(a => a.EntityType == entityType && a.EntityId == entityId), query, cancellationToken);

    public async Task<IReadOnlyList<TimelineEventDto>> GetContactTimelineAsync(string companyId, string contactId, CancellationToken cancellationToken = default)
    {
        var exists = await db.Contacts.AnyAsync(c => c.Id == contactId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Contact not found");
        }

        var dealIds = await db.Deals.Where(d => d.ContactId == contactId).Select(d => d.Id).ToListAsync(cancellationToken);
        var taskIds = await db.Tasks.Where(t => t.ContactId == contactId).Select(t => t.Id).ToListAsync(cancellationToken);

        var activity = await db.ActivityLogs.Include(a => a.User).Where(a =>
                (a.EntityType == "Contact" && a.EntityId == contactId) ||
                (a.EntityType == "Deal" && dealIds.Contains(a.EntityId)) ||
                (a.EntityType == "TaskItem" && taskIds.Contains(a.EntityId)))
            .ToListAsync(cancellationToken);

        var communications = await db.Communications.Where(cm => cm.ContactId == contactId).ToListAsync(cancellationToken);

        return Merge(activity, communications);
    }

    public async Task<IReadOnlyList<TimelineEventDto>> GetDealTimelineAsync(string companyId, string dealId, CancellationToken cancellationToken = default)
    {
        var deal = await db.Deals.SingleOrDefaultAsync(d => d.Id == dealId, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        var taskIds = await db.Tasks.Where(t => t.DealId == dealId).Select(t => t.Id).ToListAsync(cancellationToken);

        var activity = await db.ActivityLogs.Include(a => a.User).Where(a =>
                (a.EntityType == "Deal" && a.EntityId == dealId) ||
                (a.EntityType == "Contact" && a.EntityId == deal.ContactId) ||
                (a.EntityType == "TaskItem" && taskIds.Contains(a.EntityId)))
            .ToListAsync(cancellationToken);

        var communications = await db.Communications.Where(cm => cm.ContactId == deal.ContactId).ToListAsync(cancellationToken);

        return Merge(activity, communications);
    }

    public async Task<IReadOnlyList<TimelineEventDto>> GetTaskTimelineAsync(string companyId, string taskId, CancellationToken cancellationToken = default)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            ?? throw new NotFoundException("Task not found");

        var activity = await db.ActivityLogs.Include(a => a.User).Where(a =>
                (a.EntityType == "TaskItem" && a.EntityId == taskId) ||
                (task.DealId != null && a.EntityType == "Deal" && a.EntityId == task.DealId) ||
                (task.ContactId != null && a.EntityType == "Contact" && a.EntityId == task.ContactId))
            .ToListAsync(cancellationToken);

        var communications = task.ContactId is null
            ? []
            : await db.Communications.Where(cm => cm.ContactId == task.ContactId).ToListAsync(cancellationToken);

        return Merge(activity, communications);
    }

    private static IReadOnlyList<TimelineEventDto> Merge(List<ActivityLog> activity, List<Communication> communications)
    {
        var events = new List<TimelineEventDto>(activity.Count + communications.Count);
        events.AddRange(activity.Select(a => new TimelineEventDto("ActivityLog", a.EntityType, a.EntityId, a.Action, a.ChangesJson, a.UserId, a.User?.Name, a.CreatedAt)));
        events.AddRange(communications.Select(cm => new TimelineEventDto(
            "Communication", "Communication", cm.Id, $"{cm.Channel} {cm.Direction}", cm.Content, null, null, cm.Timestamp)));

        return events.OrderByDescending(e => e.Timestamp).ToList();
    }

    private static async Task<PagedResult<ActivityLogEntryDto>> PageAsync(IQueryable<ActivityLog> source, QueryActivityLogRequest query, CancellationToken cancellationToken)
    {
        var total = await source.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await source
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<ActivityLogEntryDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    private static ActivityLogEntryDto ToDto(ActivityLog a) => new(
        a.Id, a.EntityType, a.EntityId, a.UserId, a.User?.Name, a.Action, a.ChangesJson, a.CreatedAt);
}
