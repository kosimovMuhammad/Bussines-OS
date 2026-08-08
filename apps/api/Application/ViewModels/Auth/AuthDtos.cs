using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels.Users;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Auth;

public record RegisterRequest(string CompanyName, string Name, string Email, string Password, string Role);

public record LoginRequest(string Email, string Password);

public record LoginWithCodeRequest(string Email, string Code);

public record VerifyEmailRequest(string Email, string Code);

public record ResendVerificationRequest(string Email);

public record ResendInviteCodeRequest(string Email);

public record RegistrationPendingDto(string Email, bool RequiresVerification);

public record AuthTokens(string AccessToken, string RefreshToken, UserDto User);

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Role).NotEmpty().Must(r => OnboardingRoles.All.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", OnboardingRoles.All)}");
    }
}

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

public class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginWithCodeRequestValidator : AbstractValidator<LoginWithCodeRequest>
{
    public LoginWithCodeRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

public class ResendInviteCodeRequestValidator : AbstractValidator<ResendInviteCodeRequest>
{
    public ResendInviteCodeRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
