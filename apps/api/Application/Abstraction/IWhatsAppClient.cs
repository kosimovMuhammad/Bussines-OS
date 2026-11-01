using BusinessOS.Application.ViewModels.WhatsApp;

namespace BusinessOS.Application.Abstraction;

public interface IWhatsAppClient
{
    // WhatsApp Embedded Signup (mirrors IInstagramApiClient's OAuth methods): the frontend's
    // FB.login() popup returns a one-time code with no redirect_uri involved (unlike the
    // Instagram flow, this is a pure JS-SDK grant, not a browser redirect) - ExchangeCodeAsync
    // trades it for a business token server-side, client_secret never reaches the browser.
    // The code lives only ~30 seconds.
    Task<WhatsAppOAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);

    // WhatsApp/Facebook has no separate "refresh" endpoint like Instagram's ig_refresh_token -
    // re-exchanging the current (still-valid) long-lived token via fb_exchange_token again is
    // both the initial upgrade AND the periodic renewal (see WhatsAppTokenRefreshService).
    // Whether an Embedded Signup business token needs/accepts this is still to be confirmed
    // empirically (Phase 2 live test).
    Task<WhatsAppOAuthTokenResult> ExchangeForLongLivedTokenAsync(string token, CancellationToken cancellationToken = default);

    // GET /debug_token, authenticated with the app access token (app_id|app_secret): returns the
    // token's user id, expiry and granular scopes. The granular scopes are how the server learns
    // which WABA(s) the token can reach instead of trusting browser-reported ids.
    Task<WhatsAppTokenInfo> DebugTokenAsync(string inputToken, CancellationToken cancellationToken = default);

    // GET /{waba}/phone_numbers - the numbers that belong to a WABA the token can see.
    Task<IReadOnlyList<WhatsAppPhoneNumberInfo>> GetWabaPhoneNumbersAsync(string accessToken, string wabaId, CancellationToken cancellationToken = default);

    // GET /{phone_number_id}?fields=verified_name,display_phone_number,platform_type,is_on_biz_app,status.
    // Throws if Meta rejects the call (unlike GetVerifiedNameAsync, which swallows errors).
    Task<WhatsAppPhoneNumberInfo> GetPhoneNumberAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default);

    // POST /{phone_number_id}/register {messaging_product, pin}. Standard (non-coexistence)
    // numbers only - coexistence numbers are already registered. Meta allows 10 calls per
    // number per 72 hours (error 133016 beyond that), so callers must not loop on this.
    Task RegisterPhoneNumberAsync(string accessToken, string phoneNumberId, string pin, CancellationToken cancellationToken = default);

    // Per-WABA subscription so this app receives webhooks for a customer's own WhatsApp
    // Business Account (Tech Provider model - the same reasoning as
    // IInstagramApiClient.SubscribeWebhookAsync's per-account subscription).
    Task SubscribeWebhookAsync(string accessToken, string wabaId, CancellationToken cancellationToken = default);

    // DELETE /{waba}/subscribed_apps - best-effort cleanup on disconnect.
    Task UnsubscribeWebhookAsync(string accessToken, string wabaId, CancellationToken cancellationToken = default);

    // POST /{phone_number_id}/smb_app_data {messaging_product, sync_type}. Coexistence only; must
    // be called within 24h of onboarding, once per sync type (see WhatsAppSyncTypes). Returns
    // Meta's request_id (keep it for support) - success only means the request was accepted.
    Task<string?> RequestSmbAppDataSyncAsync(string accessToken, string phoneNumberId, string syncType, CancellationToken cancellationToken = default);

    Task<string?> GetVerifiedNameAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default);

    Task<string?> SendTextMessageAsync(string phoneNumberId, string accessToken, string to, string message, CancellationToken cancellationToken = default);

    // Platform mode: credentials/phone number id are resolved internally from
    // WhatsAppPlatformOptions + the WHATSAPP_ACCESS_TOKEN env var / user secret, not from a
    // per-company WhatsAppAccount row - callers never see or pass a token.
    Task<string?> SendPlatformTextMessageAsync(string to, string message, CancellationToken cancellationToken = default);

    Task<string?> SendPlatformTemplateMessageAsync(string to, string templateName, string languageCode, CancellationToken cancellationToken = default);
}
