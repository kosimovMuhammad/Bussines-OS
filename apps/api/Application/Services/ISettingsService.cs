using BusinessOS.Application.ViewModels.Settings;

namespace BusinessOS.Application.Services;

public interface ISettingsService
{
    Task<AllSettingsDto> GetAllAsync(string companyId, CancellationToken cancellationToken = default);
    Task<AllSettingsDto> UpdateAllAsync(string companyId, string actorUserId, UpdateAllSettingsRequest request, CancellationToken cancellationToken = default);

    Task<CompanySettingsDto> GetCompanyAsync(string companyId, CancellationToken cancellationToken = default);
    Task<CompanySettingsDto> UpdateCompanyAsync(string companyId, string actorUserId, UpdateCompanySettingsRequest request, CancellationToken cancellationToken = default);

    Task<SecuritySettingsDto> GetSecurityAsync(string companyId, CancellationToken cancellationToken = default);
    Task<SecuritySettingsDto> UpdateSecurityAsync(string companyId, string actorUserId, UpdateSecuritySettingsRequest request, CancellationToken cancellationToken = default);

    Task<BrandingSettingsDto> GetBrandingAsync(string companyId, CancellationToken cancellationToken = default);
    Task<BrandingSettingsDto> UpdateBrandingAsync(string companyId, string actorUserId, UpdateBrandingSettingsRequest request, CancellationToken cancellationToken = default);
}
