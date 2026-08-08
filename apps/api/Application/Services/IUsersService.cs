using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface IUsersService
{
    Task<UserDto> GetMeAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateMeAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task SetPasswordAsync(string userId, SetPasswordRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> ListAsync(string companyId, CancellationToken cancellationToken = default);
    Task<UserDto> GetOneAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<UserDto> InviteAsync(string companyId, string actorUserId, InviteUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> ResendInviteAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateAsync(string companyId, string actorUserId, Role actorRole, string id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateRoleAsync(string companyId, string actorUserId, Role actorRole, string id, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string actorUserId, string id, CancellationToken cancellationToken = default);
    Task<PagedResult<LoginHistoryDto>> GetMyLoginHistoryAsync(string userId, QueryLoginHistoryRequest query, CancellationToken cancellationToken = default);
    Task<PagedResult<LoginHistoryDto>> GetLoginHistoryAsync(string companyId, Role actorRole, string id, QueryLoginHistoryRequest query, CancellationToken cancellationToken = default);

    Task<BulkInviteResult> BulkInviteAsync(string companyId, string actorUserId, IReadOnlyList<InviteUserRequest> users, CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(string companyId, CancellationToken cancellationToken = default);
    Task<ImportResultDto> ImportCsvAsync(string companyId, string actorUserId, string csvContent, CancellationToken cancellationToken = default);
}
