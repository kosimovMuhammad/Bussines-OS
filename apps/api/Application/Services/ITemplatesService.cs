using BusinessOS.Application.ViewModels.Finance;

namespace BusinessOS.Application.Services;

public interface ITemplatesService
{
    Task<IReadOnlyList<TemplateDto>> ListAsync(string companyId, QueryTemplatesRequest query, CancellationToken cancellationToken = default);
    Task<TemplateDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<TemplateDto> CreateAsync(string companyId, CreateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TemplateDto> UpdateAsync(string companyId, string id, UpdateTemplateRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
