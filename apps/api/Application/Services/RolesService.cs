using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Rbac;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class RolesService(IAppDbContext db, IAuditLogService auditLog) : IRolesService
{
    public async Task<IReadOnlyList<RoleDto>> ListAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var roles = await db.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(ToDto).ToList();
    }

    public async Task<RoleDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var role = await FindAsync(id, cancellationToken);
        return ToDto(role);
    }

    public async Task<RoleDto> CreateAsync(string companyId, string actorUserId, CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var nameTaken = await db.Roles.AnyAsync(r => r.CompanyId == companyId && r.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            throw new ConflictException("A role with this name already exists");
        }

        var permissionIds = (request.PermissionIds ?? []).Distinct().ToList();
        await EnsurePermissionsExistAsync(permissionIds, cancellationToken);

        var role = new RbacRole
        {
            CompanyId = companyId,
            Name = request.Name,
            Description = request.Description,
            IsSystemRole = false,
        };

        db.Roles.Add(role);
        foreach (var permissionId in permissionIds)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId, CompanyId = companyId });
        }

        auditLog.Log(companyId, actorUserId, "permission.role_created", "Role", role.Id, new { role.Name, permissionCount = permissionIds.Count });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(await FindAsync(role.Id, cancellationToken));
    }

    public async Task<RoleDto> UpdateAsync(string companyId, string actorUserId, string id, UpdateRoleDetailsRequest request, CancellationToken cancellationToken = default)
    {
        var role = await FindAsync(id, cancellationToken);

        if (request.Name is not null && request.Name != role.Name)
        {
            if (role.IsSystemRole)
            {
                throw new ConflictException("Cannot rename a system role");
            }

            var nameTaken = await db.Roles.AnyAsync(r => r.CompanyId == companyId && r.Name == request.Name && r.Id != id, cancellationToken);
            if (nameTaken)
            {
                throw new ConflictException("A role with this name already exists");
            }

            role.Name = request.Name;
        }

        if (request.Description is not null)
        {
            role.Description = request.Description;
        }

        role.UpdatedAt = DateTime.UtcNow;
        auditLog.Log(companyId, actorUserId, "permission.role_updated", "Role", role.Id, new { role.Name, role.Description });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(role);
    }

    public async Task RemoveAsync(string companyId, string actorUserId, string id, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Role not found");

        if (role.IsSystemRole)
        {
            throw new ConflictException("Cannot delete a system role");
        }

        var hasUsers = await db.Users.AnyAsync(u => u.RbacRoleId == id, cancellationToken);
        if (hasUsers)
        {
            throw new ConflictException("Cannot delete a role with users assigned to it");
        }

        db.Roles.Remove(role);
        auditLog.Log(companyId, actorUserId, "permission.role_deleted", "Role", id, new { role.Name });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RoleDto> AttachPermissionAsync(string companyId, string actorUserId, string roleId, string permissionId, CancellationToken cancellationToken = default)
    {
        var role = await FindAsync(roleId, cancellationToken);
        await EnsurePermissionsExistAsync([permissionId], cancellationToken);

        var alreadyAttached = await db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, cancellationToken);
        if (alreadyAttached)
        {
            throw new ConflictException("This permission is already attached to the role");
        }

        db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId, CompanyId = companyId });
        auditLog.Log(companyId, actorUserId, "permission.granted", "Role", roleId, new { role.Name, permissionId });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(await FindAsync(roleId, cancellationToken));
    }

    public async Task DetachPermissionAsync(string companyId, string actorUserId, string roleId, string permissionId, CancellationToken cancellationToken = default)
    {
        var rolePermission = await db.RolePermissions.SingleOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, cancellationToken)
            ?? throw new NotFoundException("This permission is not attached to the role");

        db.RolePermissions.Remove(rolePermission);
        auditLog.Log(companyId, actorUserId, "permission.revoked", "Role", roleId, new { permissionId });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<RbacRole> FindAsync(string id, CancellationToken cancellationToken) =>
        await db.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new NotFoundException("Role not found");

    private async Task EnsurePermissionsExistAsync(List<string> permissionIds, CancellationToken cancellationToken)
    {
        if (permissionIds.Count == 0)
        {
            return;
        }

        var validCount = await db.Permissions.CountAsync(p => permissionIds.Contains(p.Id), cancellationToken);
        if (validCount != permissionIds.Count)
        {
            throw new NotFoundException("One or more permissions not found");
        }
    }

    private static RoleDto ToDto(RbacRole role) => new(
        role.Id,
        role.Name,
        role.Description,
        role.IsSystemRole,
        role.RolePermissions
            .OrderBy(rp => rp.Permission.Key)
            .Select(rp => new PermissionDto(rp.Permission.Id, rp.Permission.Key, rp.Permission.Module, rp.Permission.Description))
            .ToList(),
        role.CreatedAt,
        role.UpdatedAt);
}
