using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Inventory;

namespace BusinessOS.Application.Services;

public interface ISuppliersService
{
    Task<PagedResult<SupplierDto>> ListAsync(string companyId, QuerySuppliersRequest query, CancellationToken cancellationToken = default);
    Task<SupplierDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<SupplierDto> CreateAsync(string companyId, CreateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierDto> UpdateAsync(string companyId, string id, UpdateSupplierRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
