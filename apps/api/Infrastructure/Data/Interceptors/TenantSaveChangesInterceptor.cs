using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BusinessOS.Infrastructure.Data.Interceptors;

// Mirrors the Prisma tenant-scoping middleware's create/update/delete side:
// stamps CompanyId on inserts, and rejects mutations against a row that
// doesn't belong to the caller's tenant. Read-side scoping is handled
// separately by the EF Core global query filters in DataContext.
public class TenantSaveChangesInterceptor(ICurrentTenantContext tenantContext) : SaveChangesInterceptor
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

    private void Enforce(DbContext? context)
    {
        if (context is null || tenantContext.CompanyId is null)
        {
            return;
        }

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not ITenantScoped scoped)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added when string.IsNullOrEmpty(scoped.CompanyId):
                    scoped.CompanyId = tenantContext.CompanyId;
                    break;
                case EntityState.Modified or EntityState.Deleted when scoped.CompanyId != tenantContext.CompanyId:
                    throw new ForbiddenException("Ин захира ба company-и шумо тааллуқ надорад");
            }
        }
    }
}
