using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Application.Services;

// One generic implementation shared across every IArchivable entity, rather than
// duplicating "load, flip IsArchived, save" nine times - this codebase has no shared base
// controller/service class to hang that on (every module's service/controller is bespoke),
// so a small generic service reached via IAppDbContext.Set<TEntity>() is the reuse point
// instead. Each controller still has its own two thin actions (per-entity route +
// [RequireModule]), but the actual mutation logic lives here once.
public interface IArchiveService
{
    Task ArchiveAsync<TEntity>(string companyId, string id, CancellationToken cancellationToken = default) where TEntity : class, IArchivable;
    Task RestoreAsync<TEntity>(string companyId, string id, CancellationToken cancellationToken = default) where TEntity : class, IArchivable;
}
