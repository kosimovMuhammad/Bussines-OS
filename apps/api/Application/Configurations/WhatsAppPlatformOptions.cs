namespace BusinessOS.Application.Configurations;

// Bound from the same flat WHATSAPP_* keys every other integration in this codebase reads
// (env vars / user secrets, never appsettings.json) - see Infrastructure/DependencyInjection.cs.
public class WhatsAppPlatformOptions
{
    // The ONE Graph API version used for every WhatsApp call (OAuth, onboarding, tenant and
    // platform sends). Overridable via WHATSAPP_API_VERSION so a Meta version sunset needs no
    // redeploy; previously tenant calls were pinned to a stale hardcoded version.
    public const string DefaultApiVersion = "v26.0";

    // Main Meta app id (WHATSAPP_APP_ID) - handed to the frontend for FB.init and checked against the
    // app_id debug_token reports, so a token minted for some other app is never accepted.
    public string AppId { get; set; } = string.Empty;

    public string PhoneNumberId { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = DefaultApiVersion;

    // Facebook Login for Business configuration id ("WhatsApp Embedded Signup" variation) that
    // the frontend passes as FB.login's config_id - WHATSAPP_ES_CONFIG_ID.
    public string EmbeddedSignupConfigurationId { get; set; } = string.Empty;

    // Master switch (WHATSAPP_EMBEDDED_SIGNUP_ENABLED, default false). While the Meta app is not yet
    // eligible to onboard customers (Tech Provider / business verification pending), the config
    // endpoint answers not_configured and the UI shows a "coming soon" state. All Embedded Signup
    // code stays in place; flip this to true once Meta unblocks the app (docs/WHATSAPP_STATUS.md).
    public bool EmbeddedSignupEnabled { get; set; }
}
