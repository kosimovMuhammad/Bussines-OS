using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.WhatsApp;

// ---- Embedded Signup (JS SDK popup) ----------------------------------------------------------

// GET /whatsapp/embedded-signup/config: everything the frontend needs to call FB.login, so no
// Meta ids live in frontend env. State is HMAC-signed (user + company + locale, 10 min) and must be
// echoed back to /complete.
public record WhatsAppEmbeddedSignupConfigDto(string AppId, string ConfigurationId, string GraphVersion, string SignupVersion, string State);

// The WA_EMBEDDED_SIGNUP message event, as relayed by the frontend. EVERY field is a hint only -
// the server derives the real WABA / phone number from the token (debug_token + /phone_numbers)
// and rejects hints that don't match what the token can access. v5 reports messaging_account_ids
// (replacing waba_id); older versions report wabaId.
public record WhatsAppSignupSessionInfo(
    string? Event, string? PhoneNumberId, string? WabaId, IReadOnlyList<string>? MessagingAccountIds, string? BusinessId, bool? IsOnBizApp);

public record CompleteWhatsAppEmbeddedSignupRequest(string Code, string State, WhatsAppSignupSessionInfo? Session);

// Issues are stable codes (WhatsAppOnboardingErrors) for post-signup steps that failed; the account
// is still saved (ConnectionStatus = NeedsAttention) and POST /whatsapp/accounts/{id}/retry re-runs them.
public record WhatsAppConnectResultDto(WhatsAppAccountDto Account, IReadOnlyList<string> Issues);

// Popup CANCEL / ERROR events reported by the frontend, so failures (and Meta's session id, which
// Meta support asks for) are logged server-side. The response maps Meta's code to our stable code.
public record WhatsAppSignupClientEventRequest(string Event, string? CurrentStep, string? ErrorCode, string? ErrorMessage, string? SessionId);

public record WhatsAppSignupClientEventResultDto(string Code, string Message);

public record SendWhatsAppRequest(string ContactId, string Message);

// DTOs serialize camelCase (ASP.NET default) and bind camelCase case-insensitively - a
// snake_case body (phone_number_id) does NOT bind. Enums serialize as strings
// (JsonStringEnumConverter in Program.cs).
public record WhatsAppAccountDto(
    string Id,
    string CompanyId,
    string PhoneNumberId,
    string WabaId,
    string? VerifiedName,
    string? DisplayPhoneNumber,
    bool IsActive,
    bool IsOnBizApp,
    WhatsAppConnectionStatus ConnectionStatus,
    WhatsAppHistorySyncStatus HistorySyncStatus,
    DateTime? OnboardedAt,
    DateTime? TokenExpiresAt,
    DateTime CreatedAt);

public record IncomingWhatsAppMessage(string ProviderMessageId, string PhoneNumberId, string From, string Text, DateTime Timestamp);

// Mirrors InstagramOAuthTokenResult - ExpiresAt is null for the short-lived token from
// ExchangeCodeAsync, set for the long-lived token from ExchangeForLongLivedTokenAsync.
public record WhatsAppOAuthTokenResult(string AccessToken, DateTime? ExpiresAt);

// One entry of debug_token's data.granular_scopes: the assets (WABA ids for the whatsapp_*
// scopes) the token's user actually granted this app. This - not anything the browser
// reports - is the source of truth for which WABA a freshly minted token may touch.
public record WhatsAppGranularScope(string Scope, IReadOnlyList<string> TargetIds);

// Result of GET /debug_token. ExpiresAt is null when Meta reports expires_at = 0 (never expires).
public record WhatsAppTokenInfo(
    bool IsValid, string? AppId, string? UserId, DateTime? ExpiresAt, IReadOnlyList<WhatsAppGranularScope> GranularScopes)
{
    public IReadOnlyList<string> TargetIdsFor(string scope) =>
        GranularScopes.Where(s => s.Scope == scope).SelectMany(s => s.TargetIds).Distinct().ToList();
}

// A phone number as Meta describes it. IsOnBizApp / PlatformType are null when Meta omits them.
public record WhatsAppPhoneNumberInfo(
    string Id, string? DisplayPhoneNumber, string? VerifiedName, string? PlatformType, bool? IsOnBizApp, string? Status);

// smb_app_data sync types for Coexistence onboarding (each may be requested exactly once).
public static class WhatsAppSyncTypes
{
    public const string Contacts = "smb_app_state_sync";
    public const string History = "history";
}

public class CompleteWhatsAppEmbeddedSignupRequestValidator : AbstractValidator<CompleteWhatsAppEmbeddedSignupRequest>
{
    public CompleteWhatsAppEmbeddedSignupRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.State).NotEmpty().MaximumLength(2048);
    }
}

public class WhatsAppSignupClientEventRequestValidator : AbstractValidator<WhatsAppSignupClientEventRequest>
{
    public WhatsAppSignupClientEventRequestValidator()
    {
        RuleFor(x => x.Event).NotEmpty().Must(e => e is "CANCEL" or "ERROR").WithMessage("Event must be CANCEL or ERROR");
        RuleFor(x => x.CurrentStep).MaximumLength(100);
        RuleFor(x => x.ErrorCode).MaximumLength(50);
        RuleFor(x => x.ErrorMessage).MaximumLength(1000);
        RuleFor(x => x.SessionId).MaximumLength(200);
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
