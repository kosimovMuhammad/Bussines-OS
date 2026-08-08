using BusinessOS.Application.ViewModels.Inventory;

namespace BusinessOS.Application.Services;

public interface ICategoriesService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(string companyId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<CategoryDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateAsync(string companyId, CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateAsync(string companyId, string id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
