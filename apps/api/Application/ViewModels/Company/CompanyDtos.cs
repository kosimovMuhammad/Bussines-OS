using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Company;

public record CompanyDto(string Id, string Name, string? Industry, string Plan, string Timezone, DateTime CreatedAt);

public record UpdateCompanyRequest(string? Name, string? Industry, string? Timezone);

public record UpdatePlanRequest(Plan Plan);

public record IntegrationStatus(bool Connected, bool ComingSoon);

public record IntegrationsStatusDto(
    IntegrationStatus Whatsapp,
    IntegrationStatus Gmail,
    IntegrationStatus Outlook,
    IntegrationStatus Telegram,
    IntegrationStatus Stripe,
    IntegrationStatus Paypal);

public class UpdateCompanyRequestValidator : AbstractValidator<UpdateCompanyRequest>
{
    public UpdateCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
    }
}

public class UpdatePlanRequestValidator : AbstractValidator<UpdatePlanRequest>
{
    public UpdatePlanRequestValidator()
    {
        RuleFor(x => x.Plan).IsInEnum();
    }
}
