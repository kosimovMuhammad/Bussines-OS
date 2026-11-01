using BusinessOS.Application.ViewModels.WhatsApp;

namespace BusinessOS.Application.Services;

// WhatsApp Embedded Signup (Facebook Login for Business popup) - the ONLY way a WhatsApp number
// gets connected. Covers both standard Cloud API numbers and Coexistence numbers (already active
// in the WhatsApp Business app).
public interface IWhatsAppOnboardingService
{
    // Everything FB.login needs plus a signed, short-lived state binding the flow to this user + company.
    WhatsAppEmbeddedSignupConfigDto GetConfig(string userId, string companyId, string? locale);

    // Exchanges the popup's one-time code, derives the WABA / phone number SERVER-SIDE from the
    // resulting token (browser-reported ids are only hints), then registers (standard numbers
    // only), subscribes the app to the WABA and saves the account.
    Task<WhatsAppConnectResultDto> CompleteAsync(string companyId, string userId, CompleteWhatsAppEmbeddedSignupRequest request, CancellationToken cancellationToken = default);

    // Re-runs the post-signup steps (register / subscribe) for an account left in NeedsAttention.
    Task<WhatsAppConnectResultDto> RetryAsync(string companyId, string userId, string accountId, CancellationToken cancellationToken = default);

    // Logs a popup CANCEL / ERROR event and maps Meta's error code to our stable code.
    WhatsAppSignupClientEventResultDto ReportClientEvent(string companyId, string userId, WhatsAppSignupClientEventRequest request);
}
