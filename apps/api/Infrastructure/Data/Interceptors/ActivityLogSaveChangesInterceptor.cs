using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Infrastructure.Data.Interceptors;

// Captures create/update/delete on any IActivityLoggable entity (Contact, Deal, TaskItem
// today) as an ActivityLog row, staged in the same SavingChangesAsync call so it commits
// atomically with the change it's describing - no second write, no risk of logging a
// change that then rolls back. This is the canonical EF Core audit-interceptor pattern:
// entities added to the ChangeTracker inside SavingChanges/SavingChangesAsync are picked
// up by the same SaveChanges call (same technique TenantSaveChangesInterceptor already
// relies on for mutating tracked entries).
//
// This is also the ONE shared change-capture mechanism for the Automation Engine's
// trigger matching (see Automation Engine Phase 3 plan) - after a save actually commits
// (SavedChanges/SavedChangesAsync, never before), the same diffs captured here are
// queued as EntityChangedEvents via the existing IBackgroundTaskQueue, off the request
// thread, so trigger-matching and action execution never slow down the original save.
public class ActivityLogSaveChangesInterceptor(ICurrentUserContext currentUser, IBackgroundTaskQueue backgroundQueue) : SaveChangesInterceptor
{
    private readonly List<EntityChangedEvent> pendingEvents = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchPendingEventsAsync().GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchPendingEventsAsync();
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        pendingEvents.Clear();

        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is IActivityLoggable && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var loggable = (IActivityLoggable)entry.Entity;
            var changes = BuildChanges(entry);

            if (entry.State == EntityState.Modified && changes.Count == 0)
            {
                continue;
            }

            var entityType = entry.Entity.GetType().Name;
            var entityId = entry.Property("Id").CurrentValue?.ToString() ?? string.Empty;

            context.Set<ActivityLog>().Add(new ActivityLog
            {
                CompanyId = loggable.CompanyId,
                EntityType = entityType,
                EntityId = entityId,
                UserId = currentUser.UserId,
                Action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Deleted => "Deleted",
                    _ => "Updated",
                },
                ChangesJson = changes.Count > 0
                    ? JsonSerializer.Serialize(changes.ToDictionary(c => c.PropertyName, c => new { before = c.Before, after = c.After }))
                    : null,
            });

            pendingEvents.Add(new EntityChangedEvent(
                loggable.CompanyId,
                entityType,
                entityId,
                entry.State switch
                {
                    EntityState.Added => EntityChangeKind.Created,
                    EntityState.Deleted => EntityChangeKind.Deleted,
                    _ => EntityChangeKind.Updated,
                },
                changes,
                // Stamps the ambient workflow chain (if this save is happening inside a
                // workflow action's own mutation) so the loop guard can see it on the far
                // side of the queue boundary.
                WorkflowExecutionContext.Current));
        }
    }

    private async Task DispatchPendingEventsAsync()
    {
        if (pendingEvents.Count == 0)
        {
            return;
        }

        var events = pendingEvents.ToList();
        pendingEvents.Clear();

        foreach (var evt in events)
        {
            await backgroundQueue.QueueAsync(async (sp, ct) =>
            {
                var executionService = sp.GetRequiredService<IWorkflowExecutionService>();
                await executionService.HandleEntityChangedAsync(evt, ct);
            });

            // Phase 6: outbound webhook delivery is the second independent consumer of this
            // same event stream (see IWebhookDeliveryService's doc comment) - queued
            // separately so a slow/failing webhook delivery can never block or fail workflow
            // trigger matching, or vice versa.
            await backgroundQueue.QueueAsync(async (sp, ct) =>
            {
                var webhookDelivery = sp.GetRequiredService<IWebhookDeliveryService>();
                await webhookDelivery.HandleEntityChangedAsync(evt, ct);
            });
        }
    }

    private static List<EntityFieldChange> BuildChanges(EntityEntry entry)
    {
        var changes = new List<EntityFieldChange>();

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey())
            {
                continue;
            }

            var before = entry.State == EntityState.Added ? null : property.OriginalValue;
            var after = entry.State == EntityState.Deleted ? null : property.CurrentValue;

            if (entry.State == EntityState.Modified && Equals(before, after))
            {
                continue;
            }

            changes.Add(new EntityFieldChange(property.Metadata.Name, before, after));
        }

        return changes;
    }
}
