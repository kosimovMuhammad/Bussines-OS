namespace BusinessOS.Application.Configurations;

// Bound from the same flat WHATSAPP_* keys every other integration in this codebase reads
// (env vars / user secrets, never appsettings.json) - see Infrastructure/DependencyInjection.cs.
public class WhatsAppPlatformOptions
{
    public string PhoneNumberId { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "v25.0";
}
