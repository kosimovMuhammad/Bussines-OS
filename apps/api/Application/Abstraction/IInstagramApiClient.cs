using BusinessOS.Application.ViewModels.Instagram;

namespace BusinessOS.Application.Abstraction;

public interface IInstagramApiClient
{
    // OAuth authorization-code flow (mirrors IGoogleCalendarClient.GetAuthUrl/ExchangeCodeAsync)
    // - replaces asking the user to paste a raw access token, which is the actual leak surface
    // (browser history, chat logs, screenshots) rather than a fixable property of the token
    // itself. GetAuthUrl builds the Instagram consent-screen URL; ExchangeCodeAsync trades the
    // callback's one-time code for a short-lived token server-side (client_secret never reaches
    // the browser).
    string GetAuthUrl(string state);
    Task<InstagramOAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);

    // Short-lived tokens (~1 hour) from ExchangeCodeAsync must be upgraded to a long-lived
    // token (~60 days) immediately - this is what lets the connection survive without asking
    // the user to log in again. RefreshLongLivedTokenAsync then keeps renewing that long-lived
    // token indefinitely (each call resets the 60-day clock) as long as Meta hasn't revoked it -
    // see InstagramTokenRefreshService, which calls this on a schedule.
    Task<InstagramOAuthTokenResult> ExchangeForLongLivedTokenAsync(string shortLivedToken, CancellationToken cancellationToken = default);
    Task<InstagramOAuthTokenResult> RefreshLongLivedTokenAsync(string longLivedToken, CancellationToken cancellationToken = default);

    // Used to verify a pasted access token is real before we ever persist it, and to resolve
    // the Instagram-scoped account id the token actually belongs to - mirrors
    // TelegramApiClient.GetMeAsync. The id always comes from this response, never from user
    // input (see InstagramApiClient for why: it does not match what a caller might expect).
    Task<InstagramUserInfo> GetAccountInfoAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<string?> SendMessageAsync(string accessToken, string igUserId, string recipientIgsid, string text, CancellationToken cancellationToken = default);

    // Per-account subscription - opts this specific Instagram professional account into
    // receiving the "messages" webhook field, using the account's own IG access token (not an
    // app access token). The callback URL/verify token are configured once for the whole app in
    // the App Dashboard, not per call.
    Task SubscribeWebhookAsync(string accessToken, string igUserId, CancellationToken cancellationToken = default);
}
