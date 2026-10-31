using FluentValidation;

namespace BusinessOS.Application.ViewModels.WhatsApp;

public record ConnectWhatsAppAccountRequest(string PhoneNumberId, string WabaId, string AccessToken, string? VerifiedName);

// Sent by the frontend after Meta's WhatsApp Embedded Signup popup completes: Code is the
// one-time authorization code from the FB.login() callback, PhoneNumberId/WabaId come from the
// WA_EMBEDDED_SIGNUP postMessage event fired by the same popup - see WhatsAppService for what
// the backend does with them. No AccessToken here - the whole point is the raw token never
// reaches the browser.
public record HandleWhatsAppEmbeddedSignupRequest(string Code, string PhoneNumberId, string WabaId);

public record SendWhatsAppRequest(string ContactId, string Message);

public record UpdateWhatsAppAccountRequest(string? VerifiedName, bool? IsActive);

public record WhatsAppAccountDto(string Id, string CompanyId, string PhoneNumberId, string WabaId, string? VerifiedName, bool IsActive, DateTime? TokenExpiresAt, DateTime CreatedAt);

public record IncomingWhatsAppMessage(string ProviderMessageId, string PhoneNumberId, string From, string Text, DateTime Timestamp);

// Mirrors InstagramOAuthTokenResult - ExpiresAt is null for the short-lived token from
// ExchangeCodeAsync, set for the long-lived token from ExchangeForLongLivedTokenAsync.
public record WhatsAppOAuthTokenResult(string AccessToken, DateTime? ExpiresAt);

public class ConnectWhatsAppAccountRequestValidator : AbstractValidator<ConnectWhatsAppAccountRequest>
{
    public ConnectWhatsAppAccountRequestValidator()
    {
        RuleFor(x => x.PhoneNumberId).NotEmpty();
        RuleFor(x => x.WabaId).NotEmpty();
        RuleFor(x => x.AccessToken).NotEmpty();
    }
}

public class HandleWhatsAppEmbeddedSignupRequestValidator : AbstractValidator<HandleWhatsAppEmbeddedSignupRequest>
{
    public HandleWhatsAppEmbeddedSignupRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.PhoneNumberId).NotEmpty();
        RuleFor(x => x.WabaId).NotEmpty();
    }
}

public class SendWhatsAppRequestValidator : AbstractValidator<SendWhatsAppRequest>
{
    public SendWhatsAppRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
    }
}

public class UpdateWhatsAppAccountRequestValidator : AbstractValidator<UpdateWhatsAppAccountRequest>
{
    public UpdateWhatsAppAccountRequestValidator()
    {
        RuleFor(x => x.VerifiedName).NotEmpty().When(x => x.VerifiedName is not null);
    }
}
