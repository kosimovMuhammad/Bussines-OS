using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.WhatsApp;

// Sent by the frontend after Meta's WhatsApp Embedded Signup popup completes: Code is the
// one-time authorization code from the FB.login() callback, PhoneNumberId/WabaId come from the
// WA_EMBEDDED_SIGNUP postMessage event fired by the same popup - see WhatsAppService for what
// the backend does with them. No AccessToken here - the whole point is the raw token never
// reaches the browser.
//
// NOTE (to be replaced in Phase 2): the IDs are browser-reported and therefore untrusted; the
// reworked endpoint derives them server-side from the token instead.
public record HandleWhatsAppEmbeddedSignupRequest(string Code, string PhoneNumberId, string WabaId);

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
