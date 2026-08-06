using BusinessOS.Application.ViewModels.Company;

namespace BusinessOS.Application.Services;

public interface ICompanyService
{
    Task<CompanyDto> GetAsync(string companyId, CancellationToken cancellationToken = default);
    Task<CompanyDto> UpdateAsync(string companyId, UpdateCompanyRequest request, CancellationToken cancellationToken = default);
    Task<CompanyDto> UpdatePlanAsync(string companyId, UpdatePlanRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationsStatusDto> GetIntegrationsAsync(string companyId, CancellationToken cancellationToken = default);
}
