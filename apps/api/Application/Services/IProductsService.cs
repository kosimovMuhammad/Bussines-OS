using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Inventory;

namespace BusinessOS.Application.Services;

public interface IProductsService
{
    Task<PagedResult<ProductDto>> ListAsync(string companyId, QueryProductsRequest query, CancellationToken cancellationToken = default);
    Task<ProductDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(string companyId, CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateAsync(string companyId, string id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
