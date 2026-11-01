namespace BusinessOS.Application.Configurations;

// Bound from the same flat WHATSAPP_* keys every other integration in this codebase reads
// (env vars / user secrets, never appsettings.json) - see Infrastructure/DependencyInjection.cs.
public class WhatsAppPlatformOptions
{
    // The ONE Graph API version used for every WhatsApp call (OAuth, onboarding, tenant and
    // platform sends). Overridable via WHATSAPP_API_VERSION so a Meta version sunset needs no
    // redeploy; previously tenant calls were pinned to a stale hardcoded version.
    public const string DefaultApiVersion = "v26.0";

    public string PhoneNumberId { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = DefaultApiVersion;

    // Facebook Login for Business configuration id ("WhatsApp Embedded Signup" variation) that
    // the frontend passes as FB.login's config_id - WHATSAPP_ES_CONFIG_ID.
    public string EmbeddedSignupConfigurationId { get; set; } = string.Empty;
}
