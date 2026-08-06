using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Company;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class CompanyService(IAppDbContext db) : ICompanyService
{
    public async Task<CompanyDto> GetAsync(string companyId, CancellationToken cancellationToken = default) =>
        ToDto(await FindAsync(companyId, cancellationToken));

    public async Task<CompanyDto> UpdateAsync(string companyId, UpdateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var company = await FindAsync(companyId, cancellationToken);

        if (request.Name is not null) company.Name = request.Name;
        if (request.Industry is not null) company.Industry = request.Industry;
        if (request.Timezone is not null) company.Timezone = request.Timezone;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(company);
    }

    public async Task<CompanyDto> UpdatePlanAsync(string companyId, UpdatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var company = await FindAsync(companyId, cancellationToken);
        company.Plan = request.Plan;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(company);
    }

    public async Task<IntegrationsStatusDto> GetIntegrationsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var whatsappConnected = await db.WhatsAppAccounts.AnyAsync(w => w.CompanyId == companyId, cancellationToken);
        var gmailConnected = await db.OAuthConnections.AnyAsync(o => o.CompanyId == companyId && o.Provider == OAuthProvider.Google, cancellationToken);
        var outlookConnected = await db.OAuthConnections.AnyAsync(o => o.CompanyId == companyId && o.Provider == OAuthProvider.Microsoft, cancellationToken);

        return new IntegrationsStatusDto(
            Whatsapp: new IntegrationStatus(whatsappConnected, ComingSoon: false),
            Gmail: new IntegrationStatus(gmailConnected, ComingSoon: false),
            Outlook: new IntegrationStatus(outlookConnected, ComingSoon: false),
            Telegram: new IntegrationStatus(false, ComingSoon: true),
            Stripe: new IntegrationStatus(false, ComingSoon: true),
            Paypal: new IntegrationStatus(false, ComingSoon: true));
    }

    private async Task<Domain.Entities.Company> FindAsync(string companyId, CancellationToken cancellationToken) =>
        await db.Companies.SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken)
        ?? throw new NotFoundException("Company not found");

    private static CompanyDto ToDto(Domain.Entities.Company company) => new(
        company.Id, company.Name, company.Industry, company.Plan.ToString(), company.Timezone, company.CreatedAt);
}
