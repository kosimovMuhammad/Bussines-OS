using BusinessOS.Application.ViewModels.ApiKeys;

namespace BusinessOS.Application.Services;

public interface IApiKeysService
{
    Task<IReadOnlyList<ApiKeyDto>> ListAsync(string companyId, CancellationToken cancellationToken = default);
    Task<CreateApiKeyResponse> CreateAsync(string companyId, string actorUserId, CreateApiKeyRequest request, CancellationToken cancellationToken = default);
    Task<ApiKeyDto> UpdateAsync(string companyId, string id, UpdateApiKeyRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
