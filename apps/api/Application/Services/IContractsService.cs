using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Finance;

namespace BusinessOS.Application.Services;

public interface IContractsService
{
    Task<PagedResult<ContractDto>> ListAsync(string companyId, QueryContractsRequest query, CancellationToken cancellationToken = default);
    Task<ContractDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<ContractDto> CreateAsync(string companyId, CreateContractRequest request, CancellationToken cancellationToken = default);
    Task<ContractDto> UpdateAsync(string companyId, string id, UpdateContractRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);

    // Separate from UpdateAsync on purpose - signing needs to capture who/when/from-where,
    // which a plain PATCH has no way to supply meaningfully. UpdateAsync itself refuses a
    // Status->Signed transition and points callers here instead, so this is the only path
    // that can ever produce a signed contract.
    Task<ContractDto> SignAsync(string companyId, string actorUserId, string id, string? ipAddress, CancellationToken cancellationToken = default);
}
