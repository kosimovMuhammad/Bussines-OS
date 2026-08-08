using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Application.Services;

public class ArchiveService(IAppDbContext db) : IArchiveService
{
    public async Task ArchiveAsync<TEntity>(string companyId, string id, CancellationToken cancellationToken = default) where TEntity : class, IArchivable
    {
        var entity = await LoadAsync<TEntity>(companyId, id, cancellationToken);
        if (entity.IsArchived)
        {
            return;
        }

        entity.IsArchived = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync<TEntity>(string companyId, string id, CancellationToken cancellationToken = default) where TEntity : class, IArchivable
    {
        var entity = await LoadAsync<TEntity>(companyId, id, cancellationToken);
        if (!entity.IsArchived)
        {
            return;
        }

        entity.IsArchived = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    // FindAsync locates by primary key via the entity's mapped metadata (works generically
    // without needing to know the property is literally named "Id"), then the CompanyId
    // check on top is defense in depth on top of the global tenant query filter every
    // ITenantScoped entity already has - same belt-and-suspenders reasoning used elsewhere
    // (e.g. Inventory's DB check constraint alongside its application-level guard).
    private async Task<TEntity> LoadAsync<TEntity>(string companyId, string id, CancellationToken cancellationToken) where TEntity : class, IArchivable
    {
        var entity = await db.Set<TEntity>().FindAsync([id], cancellationToken);
        if (entity is null || entity.CompanyId != companyId)
        {
            throw new NotFoundException($"{typeof(TEntity).Name} not found");
        }

        return entity;
    }
}
