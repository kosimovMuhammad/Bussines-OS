using Microsoft.Extensions.Configuration;

namespace BusinessOS.WebApi.Webhooks;

// Startup validation for the Meta integrations. Both are optional, but a half-configured one is
// worse than an absent one: a missing/short verify token or app secret makes the webhook accept
// guessable credentials or silently reject every call. So once ANY key of an integration is set,
// the whole set must be present and strong, and the app refuses to start otherwise.
public static class WebhookConfigurationValidator
{
    public const int MinSecretLength = 16;

    private static readonly System.Text.RegularExpressions.Regex ApiVersionPattern = new(@"^v\d+\.\d+$");

    public static IReadOnlyList<string> Validate(IConfiguration configuration)
    {
        var errors = new List<string>();

        if (AnySet(configuration, "INSTAGRAM_APP_ID", "INSTAGRAM_APP_SECRET", "INSTAGRAM_VERIFY_TOKEN"))
        {
            RequireStrong(configuration, errors, "INSTAGRAM_APP_ID", 1, "Instagram app ID from the Business login settings");
            RequireStrong(configuration, errors, "INSTAGRAM_APP_SECRET", MinSecretLength, "Instagram app secret (also verifies webhook signatures)");
            RequireStrong(configuration, errors, "INSTAGRAM_VERIFY_TOKEN", MinSecretLength, "webhook verify token you also enter in the Meta dashboard");

            var publicUrl = configuration["PUBLIC_URL"];
            if (!string.IsNullOrEmpty(publicUrl) && !publicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("PUBLIC_URL must start with https:// when Instagram is configured - Meta only accepts HTTPS redirect URIs and webhook callbacks");
            }
        }

        if (AnySet(configuration, "WHATSAPP_APP_ID", "WHATSAPP_APP_SECRET", "WHATSAPP_VERIFY_TOKEN", "WHATSAPP_ES_CONFIG_ID"))
        {
            // WHATSAPP_APP_ID is used for the Embedded Signup code exchange and debug_token; the
            // secret both signs webhooks and authenticates those calls, so it must be the MAIN Meta
            // app's secret (not the Instagram app's, which signs Instagram webhooks only).
            RequireStrong(configuration, errors, "WHATSAPP_APP_ID", 1, "Meta app ID of the app that owns the WhatsApp product (Embedded Signup)");
            RequireStrong(configuration, errors, "WHATSAPP_APP_SECRET", MinSecretLength, "Meta app secret (verifies webhook signatures and exchanges the Embedded Signup code)");
            RequireStrong(configuration, errors, "WHATSAPP_VERIFY_TOKEN", MinSecretLength, "webhook verify token you also enter in the Meta dashboard");

            var publicUrl = configuration["PUBLIC_URL"];
            if (!string.IsNullOrEmpty(publicUrl) && !publicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("PUBLIC_URL must start with https:// when WhatsApp is configured - Meta only accepts HTTPS webhook callbacks");
            }

            // A typo like "26.0" or "latest" would otherwise only surface as an opaque Graph 400 on
            // the first onboarding or send.
            var apiVersion = configuration["WHATSAPP_API_VERSION"];
            if (!string.IsNullOrWhiteSpace(apiVersion) && !ApiVersionPattern.IsMatch(apiVersion))
            {
                errors.Add("WHATSAPP_API_VERSION must look like v26.0 (leave it unset to use the default)");
            }
        }

        return errors;
    }

    private static bool AnySet(IConfiguration configuration, params string[] keys) =>
        keys.Any(k => !string.IsNullOrWhiteSpace(configuration[k]));

    private static void RequireStrong(IConfiguration configuration, List<string> errors, string key, int minLength, string purpose)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{key} is required when this integration is configured ({purpose})");
        }
        else if (value.Length < minLength)
        {
            errors.Add($"{key} must be at least {minLength} characters ({purpose})");
        }
    }
}
