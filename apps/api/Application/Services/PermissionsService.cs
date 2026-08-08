using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Rbac;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class PermissionsService(IAppDbContext db, IAuditLogService auditLog) : IPermissionsService
{
    public async Task<IReadOnlyList<PermissionDto>> ListAsync(string? module, CancellationToken cancellationToken = default)
    {
        var query = db.Permissions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(p => p.Module == module);
        }

        var permissions = await query.OrderBy(p => p.Module).ThenBy(p => p.Key).ToListAsync(cancellationToken);
        return permissions.Select(ToDto).ToList();
    }

    public async Task<PermissionDto> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Permission not found");
        return ToDto(permission);
    }

    public async Task<PermissionDto> CreateAsync(string actorUserId, CreatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlatformAdminAsync(actorUserId, cancellationToken);

        var keyTaken = await db.Permissions.AnyAsync(p => p.Key == request.Key, cancellationToken);
        if (keyTaken)
        {
            throw new ConflictException("A permission with this key already exists");
        }

        var permission = new Permission { Key = request.Key, Module = request.Module, Description = request.Description };
        db.Permissions.Add(permission);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(permission);
    }

    public async Task<PermissionDto> UpdateAsync(string actorUserId, string id, UpdatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlatformAdminAsync(actorUserId, cancellationToken);

        var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Permission not found");

        if (request.Description is not null)
        {
            permission.Description = request.Description;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(permission);
    }

    public async Task RemoveAsync(string actorUserId, string id, CancellationToken cancellationToken = default)
    {
        await EnsurePlatformAdminAsync(actorUserId, cancellationToken);

        var permission = await db.Permissions.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Permission not found");

        db.Permissions.Remove(permission);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<EffectivePermissionsDto> GetEffectivePermissionsAsync(string companyId, string userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.Include(u => u.RbacRole)
            .SingleOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("User not found");

        var rolePermissionKeys = user.RbacRoleId is null
            ? new HashSet<string>()
            : (await db.RolePermissions
                .Where(rp => rp.RoleId == user.RbacRoleId)
                .Select(rp => rp.Permission.Key)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var overrides = await db.UserPermissionOverrides
            .Include(o => o.Permission)
            .Where(o => o.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var userOverride in overrides)
        {
            if (userOverride.Allow)
            {
                rolePermissionKeys.Add(userOverride.Permission.Key);
            }
            else
            {
                rolePermissionKeys.Remove(userOverride.Permission.Key);
            }
        }

        return new EffectivePermissionsDto(
            userId,
            user.RbacRoleId,
            user.RbacRole?.Name,
            rolePermissionKeys.OrderBy(k => k).ToList(),
            overrides
                .OrderBy(o => o.Permission.Key)
                .Select(o => new UserPermissionOverrideDto(o.PermissionId, o.Permission.Key, o.Allow))
                .ToList());
    }

    public async Task UpdateUserPermissionOverridesAsync(string companyId, string actorUserId, string userId, UpdateUserPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException("User not found");
        }

        // Full replace, so duplicate PermissionIds in the request collapse to last-wins
        // before touching the DB (otherwise two Adds for the same PermissionId would hit
        // the (UserId, PermissionId) unique index).
        var desired = request.Overrides.GroupBy(o => o.PermissionId).Select(g => g.Last()).ToList();
        var permissionIds = desired.Select(o => o.PermissionId).ToList();

        if (permissionIds.Count > 0)
        {
            var validCount = await db.Permissions.CountAsync(p => permissionIds.Contains(p.Id), cancellationToken);
            if (validCount != permissionIds.Count)
            {
                throw new NotFoundException("One or more permissions not found");
            }
        }

        var existing = await db.UserPermissionOverrides.Where(o => o.UserId == userId).ToListAsync(cancellationToken);
        foreach (var stale in existing.Where(e => permissionIds.All(id => id != e.PermissionId)))
        {
            db.UserPermissionOverrides.Remove(stale);
        }

        foreach (var item in desired)
        {
            var current = existing.SingleOrDefault(e => e.PermissionId == item.PermissionId);
            if (current is null)
            {
                db.UserPermissionOverrides.Add(new UserPermissionOverride
                {
                    UserId = userId,
                    PermissionId = item.PermissionId,
                    Allow = item.Allow,
                    CompanyId = companyId,
                });
            }
            else
            {
                current.Allow = item.Allow;
            }
        }

        auditLog.Log(companyId, actorUserId, "permission.user_override_updated", "User", userId, new { overrideCount = desired.Count });
        await db.SaveChangesAsync(cancellationToken);
    }

    // Looked up fresh from the DB rather than carried on the JWT/ICurrentUserContext -
    // IsPlatformAdmin is an internal-ops-only flag with no self-service grant path, same
    // pattern as SubscriptionPlanService.EnsurePlatformAdminAsync.
    private async Task EnsurePlatformAdminAsync(string actorUserId, CancellationToken cancellationToken)
    {
        var isPlatformAdmin = await db.Users.Where(u => u.Id == actorUserId).Select(u => u.IsPlatformAdmin).SingleOrDefaultAsync(cancellationToken);
        if (!isPlatformAdmin)
        {
            throw new ForbiddenException("Only platform admins can manage the permission catalog");
        }
    }

    private static PermissionDto ToDto(Permission p) => new(p.Id, p.Key, p.Module, p.Description);
}
