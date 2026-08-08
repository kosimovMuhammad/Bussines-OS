using BusinessOS.Application.ViewModels.Rbac;

namespace BusinessOS.Application.Services;

public interface IRolesService
{
    Task<IReadOnlyList<RoleDto>> ListAsync(string companyId, CancellationToken cancellationToken = default);
    Task<RoleDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<RoleDto> CreateAsync(string companyId, string actorUserId, CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleDto> UpdateAsync(string companyId, string actorUserId, string id, UpdateRoleDetailsRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string actorUserId, string id, CancellationToken cancellationToken = default);
    Task<RoleDto> AttachPermissionAsync(string companyId, string actorUserId, string roleId, string permissionId, CancellationToken cancellationToken = default);
    Task DetachPermissionAsync(string companyId, string actorUserId, string roleId, string permissionId, CancellationToken cancellationToken = default);
}
