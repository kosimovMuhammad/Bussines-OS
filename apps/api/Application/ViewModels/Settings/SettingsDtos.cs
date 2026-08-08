using FluentValidation;

namespace BusinessOS.Application.ViewModels.Settings;

public record CompanySettingsDto(string Name, string Timezone, string Locale, string Currency);

public record SecuritySettingsDto(
    int PasswordMinLength,
    bool PasswordRequireUppercase,
    bool PasswordRequireNumber,
    bool PasswordRequireSymbol,
    int SessionTimeoutMinutes,
    bool RequireTwoFactor);

public record BrandingSettingsDto(string? LogoUrl, string PrimaryColor);

public record AllSettingsDto(CompanySettingsDto Company, SecuritySettingsDto Security, BrandingSettingsDto Branding);

public record UpdateCompanySettingsRequest(string? Name, string? Timezone, string? Locale, string? Currency);

public record UpdateSecuritySettingsRequest(
    int? PasswordMinLength,
    bool? PasswordRequireUppercase,
    bool? PasswordRequireNumber,
    bool? PasswordRequireSymbol,
    int? SessionTimeoutMinutes,
    bool? RequireTwoFactor);

public record UpdateBrandingSettingsRequest(string? LogoUrl, string? PrimaryColor);

public record UpdateAllSettingsRequest(
    UpdateCompanySettingsRequest? Company,
    UpdateSecuritySettingsRequest? Security,
    UpdateBrandingSettingsRequest? Branding);

public class UpdateCompanySettingsRequestValidator : AbstractValidator<UpdateCompanySettingsRequest>
{
    public UpdateCompanySettingsRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Timezone).NotEmpty().When(x => x.Timezone is not null);
        RuleFor(x => x.Locale).Matches("^[a-z]{2}(-[A-Z]{2})?$").When(x => x.Locale is not null)
            .WithMessage("Locale must be a language code like 'en' or 'en-US'");
        RuleFor(x => x.Currency).Length(3).When(x => x.Currency is not null)
            .WithMessage("Currency must be a 3-letter ISO code like 'TJS'");
    }
}

public class UpdateSecuritySettingsRequestValidator : AbstractValidator<UpdateSecuritySettingsRequest>
{
    public UpdateSecuritySettingsRequestValidator()
    {
        RuleFor(x => x.PasswordMinLength).InclusiveBetween(6, 128).When(x => x.PasswordMinLength is not null);
        RuleFor(x => x.SessionTimeoutMinutes).InclusiveBetween(5, 10080).When(x => x.SessionTimeoutMinutes is not null);
    }
}

public class UpdateBrandingSettingsRequestValidator : AbstractValidator<UpdateBrandingSettingsRequest>
{
    public UpdateBrandingSettingsRequestValidator()
    {
        RuleFor(x => x.LogoUrl).Must(url => Uri.TryCreate(url, UriKind.Absolute, out _)).When(x => x.LogoUrl is not null)
            .WithMessage("LogoUrl must be an absolute URL");
        RuleFor(x => x.PrimaryColor).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.PrimaryColor is not null)
            .WithMessage("PrimaryColor must be a hex color like '#2563EB'");
    }
}

public class UpdateAllSettingsRequestValidator : AbstractValidator<UpdateAllSettingsRequest>
{
    public UpdateAllSettingsRequestValidator()
    {
        RuleFor(x => x.Company!).SetValidator(new UpdateCompanySettingsRequestValidator()).When(x => x.Company is not null);
        RuleFor(x => x.Security!).SetValidator(new UpdateSecuritySettingsRequestValidator()).When(x => x.Security is not null);
        RuleFor(x => x.Branding!).SetValidator(new UpdateBrandingSettingsRequestValidator()).When(x => x.Branding is not null);
    }
}
