using BusinessOS.Application.Abstraction;
using BusinessOS.Application.ViewModels.Company;
using BusinessOS.Application.ViewModels.Settings;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class SettingsService(IAppDbContext db, ICompanyService companyService, IAuditLogService auditLog) : ISettingsService
{
    public async Task<AllSettingsDto> GetAllAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var company = await companyService.GetAsync(companyId, cancellationToken);
        var settings = await GetOrCreateAsync(companyId, cancellationToken);

        return new AllSettingsDto(ToCompanyDto(company, settings), ToSecurityDto(settings), ToBrandingDto(settings));
    }

    public async Task<AllSettingsDto> UpdateAllAsync(string companyId, string actorUserId, UpdateAllSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Company is not null)
        {
            await UpdateCompanyAsync(companyId, actorUserId, request.Company, cancellationToken);
        }

        if (request.Security is not null)
        {
            await UpdateSecurityAsync(companyId, actorUserId, request.Security, cancellationToken);
        }

        if (request.Branding is not null)
        {
            await UpdateBrandingAsync(companyId, actorUserId, request.Branding, cancellationToken);
        }

        return await GetAllAsync(companyId, cancellationToken);
    }

    public async Task<CompanySettingsDto> GetCompanyAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var company = await companyService.GetAsync(companyId, cancellationToken);
        var settings = await GetOrCreateAsync(companyId, cancellationToken);
        return ToCompanyDto(company, settings);
    }

    public async Task<CompanySettingsDto> UpdateCompanyAsync(string companyId, string actorUserId, UpdateCompanySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var company = await companyService.UpdateAsync(companyId, actorUserId,
            new UpdateCompanyRequest(request.Name, null, request.Timezone), cancellationToken);

        var settings = await GetOrCreateAsync(companyId, cancellationToken);
        if (request.Locale is not null) settings.Locale = request.Locale;
        if (request.Currency is not null) settings.Currency = request.Currency;
        settings.UpdatedAt = DateTime.UtcNow;

        auditLog.Log(companyId, actorUserId, "settings.company_updated", "CompanySettings", settings.Id,
            new { request.Name, request.Timezone, request.Locale, request.Currency });
        await db.SaveChangesAsync(cancellationToken);

        return ToCompanyDto(company, settings);
    }

    public async Task<SecuritySettingsDto> GetSecurityAsync(string companyId, CancellationToken cancellationToken = default) =>
        ToSecurityDto(await GetOrCreateAsync(companyId, cancellationToken));

    public async Task<SecuritySettingsDto> UpdateSecurityAsync(string companyId, string actorUserId, UpdateSecuritySettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(companyId, cancellationToken);

        if (request.PasswordMinLength is not null) settings.PasswordMinLength = request.PasswordMinLength.Value;
        if (request.PasswordRequireUppercase is not null) settings.PasswordRequireUppercase = request.PasswordRequireUppercase.Value;
        if (request.PasswordRequireNumber is not null) settings.PasswordRequireNumber = request.PasswordRequireNumber.Value;
        if (request.PasswordRequireSymbol is not null) settings.PasswordRequireSymbol = request.PasswordRequireSymbol.Value;
        if (request.SessionTimeoutMinutes is not null) settings.SessionTimeoutMinutes = request.SessionTimeoutMinutes.Value;
        if (request.RequireTwoFactor is not null) settings.RequireTwoFactor = request.RequireTwoFactor.Value;
        settings.UpdatedAt = DateTime.UtcNow;

        auditLog.Log(companyId, actorUserId, "settings.security_updated", "CompanySettings", settings.Id, request);
        await db.SaveChangesAsync(cancellationToken);

        return ToSecurityDto(settings);
    }

    public async Task<BrandingSettingsDto> GetBrandingAsync(string companyId, CancellationToken cancellationToken = default) =>
        ToBrandingDto(await GetOrCreateAsync(companyId, cancellationToken));

    public async Task<BrandingSettingsDto> UpdateBrandingAsync(string companyId, string actorUserId, UpdateBrandingSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(companyId, cancellationToken);

        if (request.LogoUrl is not null) settings.LogoUrl = request.LogoUrl;
        if (request.PrimaryColor is not null) settings.PrimaryColor = request.PrimaryColor;
        settings.UpdatedAt = DateTime.UtcNow;

        auditLog.Log(companyId, actorUserId, "settings.branding_updated", "CompanySettings", settings.Id, request);
        await db.SaveChangesAsync(cancellationToken);

        return ToBrandingDto(settings);
    }

    // Every company predates this table, so there's no seed step for it - the row is
    // created the first time anyone reads or writes settings for that company, seeded with
    // the entity's own field defaults.
    private async Task<CompanySettings> GetOrCreateAsync(string companyId, CancellationToken cancellationToken)
    {
        var settings = await db.CompanySettings.SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new CompanySettings { CompanyId = companyId };
        db.CompanySettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static CompanySettingsDto ToCompanyDto(CompanyDto company, CompanySettings settings) =>
        new(company.Name, company.Timezone, settings.Locale, settings.Currency);

    private static SecuritySettingsDto ToSecurityDto(CompanySettings s) => new(
        s.PasswordMinLength, s.PasswordRequireUppercase, s.PasswordRequireNumber, s.PasswordRequireSymbol,
        s.SessionTimeoutMinutes, s.RequireTwoFactor);

    private static BrandingSettingsDto ToBrandingDto(CompanySettings s) => new(s.LogoUrl, s.PrimaryColor);
}
