using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class RoleSeedingService(IAppDbContext db) : IRoleSeedingService
{
    public async Task<IReadOnlyDictionary<string, RbacRole>> SeedDefaultRolesAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var existingRoles = await db.Roles
            .Where(r => r.CompanyId == companyId)
            .ToDictionaryAsync(r => r.Name, cancellationToken);

        var missing = SystemRoles.Defaults.Where(d => !existingRoles.ContainsKey(d.Name)).ToList();
        if (missing.Count == 0)
        {
            return existingRoles;
        }

        var allPermissions = await db.Permissions.AsNoTracking().ToListAsync(cancellationToken);

        // Audit is sensitive governance data - Manager/Member/ReadOnly get no grant on it at
        // all by default (not even Read), unlike every other module. Matches the
        // AddSecurityAndVisibilityPhase migration's seed for pre-existing companies.
        var readPermissionIds = allPermissions
            .Where(p => p.Key.EndsWith(".read") && p.Module != nameof(ModuleName.Audit))
            .Select(p => p.Id)
            .ToList();
        var allPermissionIds = allPermissions.Select(p => p.Id).ToList();

        foreach (var (name, description, fullAccess) in missing)
        {
            var role = new RbacRole
            {
                CompanyId = companyId,
                Name = name,
                Description = description,
                IsSystemRole = true,
            };
            db.Roles.Add(role);
            existingRoles[name] = role;

            foreach (var permissionId in fullAccess ? allPermissionIds : readPermissionIds)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionId,
                    CompanyId = companyId,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return existingRoles;
    }
}
