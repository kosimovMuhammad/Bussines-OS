using BusinessOS.Application.ViewModels.Rbac;

namespace BusinessOS.Application.Services;

public interface IPermissionsService
{
    Task<IReadOnlyList<PermissionDto>> ListAsync(string? module, CancellationToken cancellationToken = default);
    Task<PermissionDto> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<PermissionDto> CreateAsync(string actorUserId, CreatePermissionRequest request, CancellationToken cancellationToken = default);
    Task<PermissionDto> UpdateAsync(string actorUserId, string id, UpdatePermissionRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string actorUserId, string id, CancellationToken cancellationToken = default);

    Task<EffectivePermissionsDto> GetEffectivePermissionsAsync(string companyId, string userId, CancellationToken cancellationToken = default);
    Task UpdateUserPermissionOverridesAsync(string companyId, string actorUserId, string userId, UpdateUserPermissionsRequest request, CancellationToken cancellationToken = default);
}
