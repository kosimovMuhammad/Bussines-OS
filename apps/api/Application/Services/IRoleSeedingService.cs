using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Services;

public interface IRoleSeedingService
{
    // Idempotent: only creates the system roles (by name) that don't already exist for the
    // company. Returns every system role for the company, keyed by name, existing or new.
    Task<IReadOnlyDictionary<string, RbacRole>> SeedDefaultRolesAsync(string companyId, CancellationToken cancellationToken = default);
}
