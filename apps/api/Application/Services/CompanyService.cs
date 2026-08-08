using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Company;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class CompanyService(IAppDbContext db, IAuditLogService auditLog, IPasswordHasher passwordHasher) : ICompanyService
{
    public async Task<CompanyDto> GetAsync(string companyId, CancellationToken cancellationToken = default) =>
        ToDto(await FindAsync(companyId, cancellationToken));

    public async Task<CompanyDto> UpdateAsync(string companyId, string actorUserId, UpdateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var company = await FindAsync(companyId, cancellationToken);

        if (request.Name is not null) company.Name = request.Name;
        if (request.Industry is not null) company.Industry = request.Industry;
        if (request.Timezone is not null) company.Timezone = request.Timezone;

        auditLog.Log(companyId, actorUserId, "settings.updated", "Company", companyId, new { request.Name, request.Industry, request.Timezone });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(company);
    }

    public async Task<CompanyDto> UpdatePlanAsync(string companyId, string actorUserId, UpdatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var company = await FindAsync(companyId, cancellationToken);
        var previousPlan = company.Plan;
        company.Plan = request.Plan;

        auditLog.Log(companyId, actorUserId, "billing.plan_changed", "Company", companyId, new { from = previousPlan.ToString(), to = request.Plan.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(company);
    }

    public async Task TransferOwnershipAsync(string companyId, string actorUserId, TransferOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await db.Users.SingleOrDefaultAsync(u => u.Id == actorUserId && u.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("User not found");

        if (actor.Role != Role.Owner)
        {
            throw new ForbiddenException("Only the current owner can transfer ownership");
        }

        if (actor.PasswordHash is null || !await passwordHasher.VerifyAsync(actor.PasswordHash, request.CurrentPassword))
        {
            throw new UnauthorizedAppException("Current password is incorrect");
        }

        if (request.NewOwnerUserId == actor.Id)
        {
            throw new ConflictException("This user is already the owner");
        }

        var newOwner = await db.Users.SingleOrDefaultAsync(u => u.Id == request.NewOwnerUserId && u.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("New owner not found in this company");

        if (newOwner.Status != UserStatus.Active)
        {
            throw new ConflictException("The new owner must be an active user");
        }

        actor.Role = Role.Admin;
        newOwner.Role = Role.Owner;

        // Single SaveChangesAsync call makes this atomic - the audit entry and both role
        // changes commit together or not at all, no explicit transaction needed.
        auditLog.Log(companyId, actor.Id, "security.ownership_transferred", "Company", companyId,
            new { previousOwnerId = actor.Id, newOwnerId = newOwner.Id, severity = "high" });

        await db.SaveChangesAsync(cancellationToken);
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
