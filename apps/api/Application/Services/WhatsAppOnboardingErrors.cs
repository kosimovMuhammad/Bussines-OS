using BusinessOS.Application.Configurations.Exceptions;

namespace BusinessOS.Application.Services;

// Single source of truth for Embedded Signup error codes. The codes are a public contract with
// the frontend (i18n keys ru/tj/en) - never rename one, only add.
public static class WhatsAppOnboardingErrors
{
    // Our own / session problems
    public const string NotConfigured = "not_configured";
    public const string InvalidState = "invalid_state";
    public const string InvalidSession = "invalid_session";
    public const string Cancelled = "cancelled";
    public const string CodeExchangeFailed = "code_exchange_failed";
    public const string InvalidToken = "invalid_token";
    public const string NoWabaAccess = "no_waba_access";
    public const string NoPhoneNumber = "no_phone_number";
    public const string AmbiguousPhoneNumber = "ambiguous_phone_number";
    public const string AssetMismatch = "asset_mismatch";
    public const string AlreadyConnected = "already_connected";
    public const string UnsupportedFlow = "unsupported_flow";

    // Meta's number-eligibility / Coexistence errors (surface in the popup's ERROR event and,
    // for a few, in Graph responses)
    public const string NumberNotEligible = "number_not_eligible";
    public const string RegionNotSupported = "region_not_supported";
    public const string BusinessAppUpdateRequired = "business_app_update_required";
    public const string NumberNotOnBusinessApp = "number_not_on_business_app";
    public const string NumberAlreadyRegistered = "number_already_registered";
    public const string AlreadySharedWithPartner = "already_shared_with_partner";
    public const string MarketingMessagesUnsupported = "marketing_messages_unsupported";

    // Post-signup steps that did not complete (the account is saved as NeedsAttention)
    public const string RegisterFailed = "register_failed";
    public const string RegisterRateLimited = "register_rate_limited";
    public const string SubscribeFailed = "subscribe_failed";

    public const string ProviderError = "provider_error";
    public const string Unknown = "unknown";

    // Meta error codes from the Embedded Signup errors reference ("WhatsApp Business app user
    // onboarding" table) plus Graph codes we can hit server-side. Anything else is provider_error.
    public static string FromMetaCode(string? metaCode) => metaCode switch
    {
        // "Your phone number isn't eligible to connect to the WhatsApp Business Platform" -
        // more activity in the WhatsApp Business app is needed before Meta allows it.
        "3441045" => NumberNotEligible,

        // "This feature is unavailable for phone numbers from this region."
        "3441042" => RegionNotSupported,

        // "Update your WhatsApp Business App to the latest version."
        "4563015" => BusinessAppUpdateRequired,

        // "Your phone number isn't registered with WhatsApp Business." / not linked to a WABA.
        "2655095" or "2655048" or "2655011" => NumberNotOnBusinessApp,

        // Already registered / still connected elsewhere (Meta says wait ~15 minutes after disconnecting).
        "3441030" or "3441049" or "2655094" or "2655082" => NumberAlreadyRegistered,

        // "The business is already sharing this WhatsApp Business account with a partner."
        "2655093" => AlreadySharedWithPartner,

        "3441047" => MarketingMessagesUnsupported,

        // Register: 10 requests / number / 72h exceeded.
        "133016" => RegisterRateLimited,

        // Expired / invalid token
        "190" => InvalidToken,

        _ => ProviderError,
    };

    public static int StatusFor(string code) => code switch
    {
        InvalidState or InvalidSession => 400,
        InvalidToken => 401,
        AlreadyConnected or NumberAlreadyRegistered or AlreadySharedWithPartner => 409,
        NotConfigured => 503,
        CodeExchangeFailed or ProviderError or SubscribeFailed or RegisterFailed or Unknown => 502,
        _ => 422,
    };

    public static string MessageFor(string code) => code switch
    {
        NotConfigured => "WhatsApp onboarding is not configured on the server.",
        InvalidState => "The connection request expired or does not belong to you. Please start again.",
        InvalidSession => "WhatsApp did not return a usable onboarding result. Please try again.",
        Cancelled => "WhatsApp connection was cancelled.",
        CodeExchangeFailed => "Could not complete the WhatsApp sign-in (the one-time code may have expired). Please try again.",
        InvalidToken => "WhatsApp rejected the access token. Please connect again.",
        NoWabaAccess => "No WhatsApp Business account was shared with this app.",
        NoPhoneNumber => "No phone number was added during signup.",
        AmbiguousPhoneNumber => "Several phone numbers were shared; please select one.",
        AssetMismatch => "The reported WhatsApp account does not match what was authorized.",
        AlreadyConnected => "This WhatsApp number is already connected to another company.",
        UnsupportedFlow => "This WhatsApp signup option is not supported.",
        NumberNotEligible => "This number isn't eligible yet. More activity in the WhatsApp Business app is needed.",
        RegionNotSupported => "This feature is unavailable for phone numbers from this region.",
        BusinessAppUpdateRequired => "Update the WhatsApp Business app to the latest version and try again.",
        NumberNotOnBusinessApp => "This number isn't registered with the WhatsApp Business app.",
        NumberAlreadyRegistered => "This number is already registered elsewhere. Disconnect it there, wait about 15 minutes and try again.",
        AlreadySharedWithPartner => "This WhatsApp Business account is already shared with another partner.",
        MarketingMessagesUnsupported => "This WhatsApp Business account uses marketing messages, which is not supported for this connection.",
        RegisterFailed => "The number was saved but could not be registered with WhatsApp. Use retry.",
        RegisterRateLimited => "WhatsApp limits registration attempts. Wait a few hours and retry.",
        SubscribeFailed => "The number was saved but webhook subscription failed. Use retry.",
        _ => "WhatsApp rejected the request. Please try again.",
    };

    public static WhatsAppOnboardingException Create(string code) =>
        new(code, MessageFor(code), StatusFor(code));

    // Maps a failed Graph call to the right stable code: Meta's own error code first, then a
    // fallback chosen by the step that failed.
    public static WhatsAppOnboardingException FromProvider(ExternalServiceException ex, string fallbackCode = ProviderError)
    {
        var mapped = ex is WhatsAppApiException { MetaErrorCode: { } metaCode } ? FromMetaCode(metaCode) : ProviderError;
        return Create(mapped == ProviderError ? fallbackCode : mapped);
    }
}
