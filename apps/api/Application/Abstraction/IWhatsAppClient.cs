using BusinessOS.Application.ViewModels.WhatsApp;

namespace BusinessOS.Application.Abstraction;

public interface IWhatsAppClient
{
    // WhatsApp Embedded Signup (mirrors IInstagramApiClient's OAuth methods): the frontend's
    // FB.login() popup returns a one-time code with no redirect_uri involved (unlike the
    // Instagram flow, this is a pure JS-SDK grant, not a browser redirect) - ExchangeCodeAsync
    // trades it for a short-lived token server-side, client_secret never reaches the browser.
    Task<WhatsAppOAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);

    // WhatsApp/Facebook has no separate "refresh" endpoint like Instagram's ig_refresh_token -
    // re-exchanging the current (still-valid) long-lived token via fb_exchange_token again is
    // both the initial upgrade AND the periodic renewal (see WhatsAppTokenRefreshService).
    Task<WhatsAppOAuthTokenResult> ExchangeForLongLivedTokenAsync(string token, CancellationToken cancellationToken = default);

    // Per-WABA subscription so this app receives webhooks for a customer's own WhatsApp
    // Business Account (Tech Provider model - the same reasoning as
    // IInstagramApiClient.SubscribeWebhookAsync's per-account subscription).
    Task SubscribeWebhookAsync(string accessToken, string wabaId, CancellationToken cancellationToken = default);

    Task<string?> GetVerifiedNameAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default);

    Task<string?> SendTextMessageAsync(string phoneNumberId, string accessToken, string to, string message, CancellationToken cancellationToken = default);

    // Platform mode: credentials/phone number id are resolved internally from
    // WhatsAppPlatformOptions + the WHATSAPP_ACCESS_TOKEN env var / user secret, not from a
    // per-company WhatsAppAccount row - callers never see or pass a token.
    Task<string?> SendPlatformTextMessageAsync(string to, string message, CancellationToken cancellationToken = default);

    Task<string?> SendPlatformTemplateMessageAsync(string to, string templateName, string languageCode, CancellationToken cancellationToken = default);
}
