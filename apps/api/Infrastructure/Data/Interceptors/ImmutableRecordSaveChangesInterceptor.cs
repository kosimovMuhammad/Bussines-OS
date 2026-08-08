using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BusinessOS.Infrastructure.Data.Interceptors;

// Defense-in-depth below the API level (which only ever exposes GET endpoints for
// IImmutableRecord entities like AuditLog/LoginHistory): rejects any attempt to
// modify or delete a row that was written once and must never change afterwards.
public class ImmutableRecordSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Enforce(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Enforce(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Enforce(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IImmutableRecord)
            {
                continue;
            }

            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new ForbiddenException("Ин сабт тағйирнопазир аст ва наметавонад иваз ё нест карда шавад");
            }
        }
    }
}
