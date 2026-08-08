using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Activity;
using BusinessOS.Application.ViewModels.Deals;

namespace BusinessOS.Application.Services;

public interface IDealsService
{
    Task<IReadOnlyList<DealDto>> ListAsync(string companyId, QueryDealsRequest query, CancellationToken cancellationToken = default);
    Task<DealDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<DealDto> CreateAsync(string companyId, CreateDealRequest request, CancellationToken cancellationToken = default);
    Task<DealDto> UpdateAsync(string companyId, string id, UpdateDealRequest request, CancellationToken cancellationToken = default);
    Task<DealDto> UpdateStageAsync(string companyId, string id, UpdateDealStageRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task<BulkOperationResult> BulkUpdateAsync(string companyId, IReadOnlyList<string> ids, UpdateDealRequest patch, CancellationToken cancellationToken = default);
    Task<BulkOperationResult> BulkDeleteAsync(string companyId, IReadOnlyList<string> ids, CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(string companyId, CancellationToken cancellationToken = default);
    Task<ImportResultDto> ImportCsvAsync(string companyId, string csvContent, CancellationToken cancellationToken = default);
}
