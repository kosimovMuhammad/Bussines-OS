using BusinessOS.Application.ViewModels.Deals;

namespace BusinessOS.Application.Services;

public interface IDealsService
{
    Task<IReadOnlyList<DealDto>> ListAsync(string companyId, QueryDealsRequest query, CancellationToken cancellationToken = default);
    Task<DealDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<DealDto> CreateAsync(string companyId, CreateDealRequest request, CancellationToken cancellationToken = default);
    Task<DealDto> UpdateAsync(string companyId, string id, UpdateDealRequest request, CancellationToken cancellationToken = default);
    Task<DealDto> UpdateStageAsync(string companyId, string id, UpdateDealStageRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
