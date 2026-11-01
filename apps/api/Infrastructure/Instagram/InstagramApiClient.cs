using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Instagram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Instagram;

// Instagram Business Messaging tokens minted via native Instagram Login are scoped to
// graph.instagram.com, not graph.facebook.com - a token accepted by one host is rejected by
// the other with "Invalid OAuth access token - Cannot parse access token" (or, for the
// app-access-token-based /{app-id}/subscriptions call, "Cannot get application info due to a
// system error" - that endpoint is the Facebook-Page-linked subscription model and does not
// apply to accounts connected via native Instagram Login). So account identity must always come
// from this response, never from user input. Webhook subscription for Instagram-Login-connected
// accounts is per-account (POST /{ig-user-id}/subscribed_apps on graph.instagram.com,
// authenticated with that account's own IG access token) rather than the app-level subscription
// used for Page-linked accounts.
//
// GET /me on graph.instagram.com returns TWO different ids and only one of them is usable:
// "id" is the app-scoped id of the token's user and is NOT what shows up in webhooks; "user_id"
// is the Instagram professional account id (<IG_ID>) and is documented by Meta as "the value of
// the id field received in webhook notifications for this account"
// (https://developers.facebook.com/docs/instagram-platform/instagram-api-with-instagram-login/get-started).
// Using "id" instead of "user_id" is exactly what causes GetAccountInfoAsync's resolved id to
// mismatch messagingEvent.Recipient.Id in InstagramService.ProcessMessagingEventAsync - always
// read UserId (falling back to Id only if Meta omits user_id for a given token), never Id, when
// persisting or looking up InstagramAccount.InstagramBusinessAccountId.
public class InstagramApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<InstagramApiClient> logger) : IInstagramApiClient
{
    // Overridable via INSTAGRAM_API_VERSION so a Meta version bump/sunset needs no redeploy.
    private const string DefaultApiVersion = "v24.0";
    private const string BaseUrl = "https://graph.instagram.com";

    // instagram_business_basic covers GetAccountInfoAsync's /me call; instagram_business_manage_messages
    // covers SendMessageAsync/SubscribeWebhookAsync - see the official Instagram-Login-with-Instagram
    // scope list linked in the class comment above.
    private const string Scope = "instagram_business_basic,instagram_business_manage_messages";

    private string ApiVersion => configuration["INSTAGRAM_API_VERSION"] is { Length: > 0 } v ? v : DefaultApiVersion;

    public string GetAuthUrl(string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = configuration["INSTAGRAM_APP_ID"];
        query["redirect_uri"] = RedirectUri();
        query["response_type"] = "code";
        query["scope"] = Scope;
        query["state"] = state;
        return $"https://www.instagram.com/oauth/authorize?{query}";
    }

    public async Task<InstagramOAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var form = new Dictionary<string, string>
        {
            ["client_id"] = configuration["INSTAGRAM_APP_ID"] ?? string.Empty,
            ["client_secret"] = configuration["INSTAGRAM_APP_SECRET"] ?? string.Empty,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri(),
            ["code"] = InstagramOAuthParser.NormalizeCode(code),
        };

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.PostAsync("https://api.instagram.com/oauth/access_token", new FormUrlEncodedContent(form), cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Instagram API request 'ExchangeCode' failed to complete");
            throw new ExternalServiceException("Instagram API request timed out or is unreachable");
        }

        if (!response.IsSuccessStatusCode)
        {
            ThrowForError(body, response.StatusCode, "ExchangeCode");
        }

        try
        {
            // Handles both {"data":[{...}]} (Instagram Login) and the flat legacy shape.
            var accessToken = InstagramOAuthParser.ParseShortLivedAccessToken(body);

            // No expires_in here - this short-lived token (~1 hour) is only ever used to
            // immediately call ExchangeForLongLivedTokenAsync, never stored.
            return new InstagramOAuthTokenResult(accessToken, null);
        }
        catch (JsonException)
        {
            throw new ExternalServiceException("Instagram API returned an unexpected token response");
        }
    }

    public Task<InstagramOAuthTokenResult> ExchangeForLongLivedTokenAsync(string shortLivedToken, CancellationToken cancellationToken = default) =>
        GetOAuthTokenAsync("https://graph.instagram.com/access_token", new Dictionary<string, string>
        {
            ["grant_type"] = "ig_exchange_token",
            ["client_secret"] = configuration["INSTAGRAM_APP_SECRET"] ?? string.Empty,
            ["access_token"] = shortLivedToken,
        }, "ExchangeForLongLivedToken", cancellationToken);

    public Task<InstagramOAuthTokenResult> RefreshLongLivedTokenAsync(string longLivedToken, CancellationToken cancellationToken = default) =>
        GetOAuthTokenAsync("https://graph.instagram.com/refresh_access_token", new Dictionary<string, string>
        {
            ["grant_type"] = "ig_refresh_token",
            ["access_token"] = longLivedToken,
        }, "RefreshLongLivedToken", cancellationToken);

    private async Task<InstagramOAuthTokenResult> GetOAuthTokenAsync(string url, Dictionary<string, string> queryParams, string method, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var query = HttpUtility.ParseQueryString(string.Empty);
        foreach (var (key, value) in queryParams)
        {
            query[key] = value;
        }

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.GetAsync($"{url}?{query}", cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Instagram API request '{Method}' failed to complete", method);
            throw new ExternalServiceException("Instagram API request timed out or is unreachable");
        }

        if (!response.IsSuccessStatusCode)
        {
            ThrowForError(body, response.StatusCode, method);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString()
                ?? throw new ExternalServiceException("Instagram API returned an empty access token");
            var expiresInSeconds = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt64() : (long?)null;
            var expiresAt = expiresInSeconds is null ? (DateTime?)null : DateTime.UtcNow.AddSeconds(expiresInSeconds.Value);
            return new InstagramOAuthTokenResult(accessToken, expiresAt);
        }
        catch (JsonException)
        {
            throw new ExternalServiceException("Instagram API returned an unexpected token response");
        }
    }

    private string RedirectUri()
    {
        var publicUrl = configuration["PUBLIC_URL"]
            ?? throw new InvalidOperationException("PUBLIC_URL is not configured (set it via user secrets or an environment variable)");
        return $"{publicUrl.TrimEnd('/')}/instagram/connect/callback";
    }

    public async Task<InstagramUserInfo> GetAccountInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["fields"] = "id,user_id,username,name";
        query["access_token"] = accessToken;

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.GetAsync($"{BaseUrl}/{ApiVersion}/me?{query}", cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Instagram API request 'GetAccountInfo' failed to complete");
            throw new ExternalServiceException("Instagram API request timed out or is unreachable");
        }

        if (!response.IsSuccessStatusCode)
        {
            ThrowForError(body, response.StatusCode, "GetAccountInfo");
        }

        try
        {
            var info = JsonSerializer.Deserialize<InstagramUserInfo>(body, JsonOptions);
            if (info is null)
            {
                throw new ExternalServiceException("Instagram API returned an empty account info result");
            }

            // Temporary diagnostic - confirms whether Meta actually returns user_id for this
            // token (it's requested in the fields list above, but some token/flow combinations
            // have been observed to omit it, silently falling back to the wrong Id - see
            // InstagramService.UpsertAccountAsync's Id vs UserId comment). Remove once confirmed.
            logger.LogInformation(
                "Instagram GetAccountInfo raw: id={Id} user_id={UserId} username={Username}",
                info.Id, info.UserId, info.Username);

            return info;
        }
        catch (JsonException)
        {
            throw new ExternalServiceException("Instagram API returned an unexpected account info response");
        }
    }

    public async Task<string?> SendMessageAsync(string accessToken, string igUserId, string recipientIgsid, string text, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["access_token"] = accessToken;

        var payload = new
        {
            recipient = new { id = recipientIgsid },
            message = new { text },
        };

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.PostAsJsonAsync($"{BaseUrl}/{ApiVersion}/{igUserId}/messages?{query}", payload, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Instagram API request 'SendMessage' failed to complete");
            throw new ExternalServiceException("Instagram API request timed out or is unreachable") { IsTimeout = true };
        }

        if (!response.IsSuccessStatusCode)
        {
            ThrowForError(body, response.StatusCode, "SendMessage", response.Headers.RetryAfter?.Delta);
        }

        return ParseMessageId(body);
    }

    public async Task SubscribeWebhookAsync(string accessToken, string igUserId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["subscribed_fields"] = "messages";
        query["access_token"] = accessToken;

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.PostAsync($"{BaseUrl}/{ApiVersion}/{igUserId}/subscribed_apps?{query}", null, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Instagram API request 'SubscribeWebhook' failed to complete");
            throw new ExternalServiceException("Instagram API request timed out or is unreachable");
        }

        if (!response.IsSuccessStatusCode)
        {
            ThrowForError(body, response.StatusCode, "SubscribeWebhook");
        }
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    private void ThrowForError(string body, System.Net.HttpStatusCode statusCode, string method, TimeSpan? retryAfter = null)
    {
        var (errorMessage, errorCode, fbTraceId) = ParseMetaError(body);

        // Never log accessToken/body verbatim - body can carry message content in echoed
        // request context (same reasoning as MetaWhatsAppClient).
        logger.LogWarning(
            "Instagram API rejected {Method}: status={StatusCode} code={Code} fbtrace_id={FbTraceId}",
            method, (int)statusCode, errorCode, fbTraceId);

        throw new ExternalServiceException(errorMessage ?? $"Instagram API request '{method}' failed: {statusCode}")
        {
            ProviderStatusCode = (int)statusCode,
            RetryAfterSeconds = retryAfter is { } delta ? (int)delta.TotalSeconds : null,
        };
    }

    private static string? ParseMessageId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("message_id", out var id) ? id.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string? Message, string? Code, string? FbTraceId) ParseMetaError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var error))
            {
                return (null, null, null);
            }

            var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
            var code = error.TryGetProperty("code", out var c) ? c.ToString() : null;
            var fbTraceId = error.TryGetProperty("fbtrace_id", out var f) ? f.GetString() : null;
            return (message, code, fbTraceId);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}
