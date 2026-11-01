using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.WhatsApp;

public class MetaWhatsAppClient(
    IHttpClientFactory httpClientFactory,
    IOptions<WhatsAppPlatformOptions> platformOptions,
    IConfiguration configuration,
    ILogger<MetaWhatsAppClient> logger) : IWhatsAppClient
{
    private const string GraphBaseUrl = "https://graph.facebook.com";

    // One configured version for every call (WHATSAPP_API_VERSION, default in WhatsAppPlatformOptions).
    private string ApiVersion => platformOptions.Value.ApiVersion;

    // Meta documents the FB.login code exchange as GET /oauth/access_token with client_id,
    // client_secret and code only (no redirect_uri). Live testing (2026-11-01) showed that when
    // Meta rejects a code with "redirect_uri is identical..." (the plain "Continue as" login path,
    // which never runs the WhatsApp screens) NO redirect_uri variant rescues it: empty, page URL,
    // origin, login_success.html and xd_arbiter were all rejected - so there is deliberately no retry.
    public async Task<WhatsAppOAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await GetOAuthTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = configuration["WHATSAPP_APP_ID"] ?? string.Empty,
            ["client_secret"] = configuration["WHATSAPP_APP_SECRET"] ?? string.Empty,
            ["code"] = code,
        }, "ExchangeCode", cancellationToken);

    public Task<WhatsAppOAuthTokenResult> ExchangeForLongLivedTokenAsync(string token, CancellationToken cancellationToken = default) =>
        GetOAuthTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "fb_exchange_token",
            ["client_id"] = configuration["WHATSAPP_APP_ID"] ?? string.Empty,
            ["client_secret"] = configuration["WHATSAPP_APP_SECRET"] ?? string.Empty,
            ["fb_exchange_token"] = token,
        }, "ExchangeForLongLivedToken", cancellationToken);

    private async Task<WhatsAppOAuthTokenResult> GetOAuthTokenAsync(Dictionary<string, string> queryParams, string method, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        var query = HttpUtility.ParseQueryString(string.Empty);
        foreach (var (key, value) in queryParams)
        {
            query[key] = value;
        }

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.GetAsync($"https://graph.facebook.com/{ApiVersion}/oauth/access_token?{query}", cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "WhatsApp API request '{Method}' failed to complete", method);
            throw new ExternalServiceException("WhatsApp API request timed out or is unreachable");
        }

        if (!response.IsSuccessStatusCode)
        {
            var (errorMessage, errorCode, fbTraceId) = ParseMetaError(body);
            logger.LogWarning("WhatsApp API rejected {Method}: status={StatusCode} code={Code} fbtrace_id={FbTraceId}",
                method, (int)response.StatusCode, errorCode, fbTraceId);
            throw new WhatsAppApiException(errorMessage ?? $"WhatsApp API request '{method}' failed: {response.StatusCode}", errorCode, fbTraceId) { ProviderStatusCode = (int)response.StatusCode };
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString()
                ?? throw new ExternalServiceException("WhatsApp API returned an empty access token");
            var expiresInSeconds = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt64() : (long?)null;
            var expiresAt = expiresInSeconds is null ? (DateTime?)null : DateTime.UtcNow.AddSeconds(expiresInSeconds.Value);
            return new WhatsAppOAuthTokenResult(accessToken, expiresAt);
        }
        catch (JsonException)
        {
            throw new ExternalServiceException("WhatsApp API returned an unexpected token response");
        }
    }

    public Task SubscribeWebhookAsync(string accessToken, string wabaId, CancellationToken cancellationToken = default) =>
        GraphAsync(HttpMethod.Post, $"{wabaId}/subscribed_apps", accessToken, null, "SubscribeWebhook", cancellationToken);

    public Task UnsubscribeWebhookAsync(string accessToken, string wabaId, CancellationToken cancellationToken = default) =>
        GraphAsync(HttpMethod.Delete, $"{wabaId}/subscribed_apps", accessToken, null, "UnsubscribeWebhook", cancellationToken);

    public async Task<WhatsAppTokenInfo> DebugTokenAsync(string inputToken, CancellationToken cancellationToken = default)
    {
        // The app access token is "{app_id}|{app_secret}"; sent as a Bearer header so it never
        // lands in a URL. input_token (the token under inspection) has to be a query parameter.
        var appToken = $"{configuration["WHATSAPP_APP_ID"]}|{configuration["WHATSAPP_APP_SECRET"]}";
        var body = await GraphAsync(HttpMethod.Get, $"debug_token?input_token={Uri.EscapeDataString(inputToken)}", appToken, null, "DebugToken", cancellationToken);

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                throw new ExternalServiceException("WhatsApp API returned an unexpected debug_token response");
            }

            var isValid = data.TryGetProperty("is_valid", out var valid) && valid.ValueKind == JsonValueKind.True;
            var appId = ReadString(data, "app_id");
            var userId = ReadString(data, "user_id");

            // expires_at = 0 means "never expires" -> null, so callers can tell it apart from a real date.
            DateTime? expiresAt = null;
            if (data.TryGetProperty("expires_at", out var exp) && exp.ValueKind == JsonValueKind.Number && exp.GetInt64() > 0)
            {
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()).UtcDateTime;
            }

            var scopes = new List<WhatsAppGranularScope>();
            if (data.TryGetProperty("granular_scopes", out var granular) && granular.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in granular.EnumerateArray())
                {
                    var scope = ReadString(entry, "scope");
                    if (scope is null)
                    {
                        continue;
                    }

                    var targets = new List<string>();
                    if (entry.TryGetProperty("target_ids", out var ids) && ids.ValueKind == JsonValueKind.Array)
                    {
                        // Meta returns these as strings, but tolerate numbers rather than dropping the asset.
                        targets.AddRange(ids.EnumerateArray()
                            .Select(i => i.ValueKind == JsonValueKind.String ? i.GetString() : i.ToString())
                            .Where(i => !string.IsNullOrWhiteSpace(i))!);
                    }

                    scopes.Add(new WhatsAppGranularScope(scope, targets));
                }
            }

            return new WhatsAppTokenInfo(isValid, appId, userId, expiresAt, scopes);
        }
        catch (JsonException)
        {
            throw new ExternalServiceException("WhatsApp API returned an unexpected debug_token response");
        }
    }

    private const string PhoneNumberFields = "id,display_phone_number,verified_name,platform_type,is_on_biz_app,status";

    public async Task<IReadOnlyList<WhatsAppPhoneNumberInfo>> GetWabaPhoneNumbersAsync(string accessToken, string wabaId, CancellationToken cancellationToken = default)
    {
        var body = await GraphAsync(HttpMethod.Get, $"{wabaId}/phone_numbers?fields={PhoneNumberFields}&limit=100", accessToken, null, "GetWabaPhoneNumbers", cancellationToken);

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return data.EnumerateArray().Select(ParsePhoneNumber).Where(p => p is not null).Select(p => p!).ToList();
        }
        catch (JsonException)
        {
            throw new ExternalServiceException("WhatsApp API returned an unexpected phone_numbers response");
        }
    }

    public async Task<WhatsAppPhoneNumberInfo> GetPhoneNumberAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default)
    {
        var body = await GraphAsync(HttpMethod.Get, $"{phoneNumberId}?fields={PhoneNumberFields}", accessToken, null, "GetPhoneNumber", cancellationToken);

        try
        {
            using var doc = JsonDocument.Parse(body);
            return ParsePhoneNumber(doc.RootElement)
                ?? throw new ExternalServiceException("WhatsApp API returned an unexpected phone number response");
        }
        catch (JsonException)
        {
            throw new ExternalServiceException("WhatsApp API returned an unexpected phone number response");
        }
    }

    public Task RegisterPhoneNumberAsync(string accessToken, string phoneNumberId, string pin, CancellationToken cancellationToken = default) =>
        GraphAsync(HttpMethod.Post, $"{phoneNumberId}/register", accessToken,
            new { messaging_product = "whatsapp", pin }, "RegisterPhoneNumber", cancellationToken);

    public async Task<string?> RequestSmbAppDataSyncAsync(string accessToken, string phoneNumberId, string syncType, CancellationToken cancellationToken = default)
    {
        var body = await GraphAsync(HttpMethod.Post, $"{phoneNumberId}/smb_app_data", accessToken,
            new { messaging_product = "whatsapp", sync_type = syncType }, "RequestSmbAppDataSync", cancellationToken);

        try
        {
            using var doc = JsonDocument.Parse(body);
            return ReadString(doc.RootElement, "request_id");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WhatsAppPhoneNumberInfo? ParsePhoneNumber(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = ReadString(element, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        bool? isOnBizApp = element.TryGetProperty("is_on_biz_app", out var biz) && biz.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? biz.GetBoolean()
            : null;

        return new WhatsAppPhoneNumberInfo(
            id, ReadString(element, "display_phone_number"), ReadString(element, "verified_name"),
            ReadString(element, "platform_type"), isOnBizApp, ReadString(element, "status"));
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind == JsonValueKind.Number ? value.ToString() : null
            : null;

    // Shared Graph call for the onboarding endpoints (the send path keeps its own SendAsync, which
    // has send-specific logging). Throws WhatsAppApiException - carrying Meta's error code and
    // fbtrace_id - on a non-2xx, so callers can map specific onboarding errors (e.g. 133016
    // register rate limit, 3441045 ineligible number) instead of showing a generic failure.
    // Neither the access token nor the response body is ever logged.
    private async Task<string> GraphAsync(HttpMethod method, string path, string bearerToken, object? jsonBody, string operation, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        using var request = new HttpRequestMessage(method, $"{GraphBaseUrl}/{ApiVersion}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        if (jsonBody is not null)
        {
            request.Content = JsonContent.Create(jsonBody);
        }

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "WhatsApp API request '{Operation}' failed to complete", operation);
            throw new ExternalServiceException("WhatsApp API request timed out or is unreachable") { IsTimeout = true };
        }

        if (!response.IsSuccessStatusCode)
        {
            var (errorMessage, errorCode, fbTraceId) = ParseMetaError(body);
            logger.LogWarning("WhatsApp API rejected {Operation}: status={StatusCode} code={Code} fbtrace_id={FbTraceId}",
                operation, (int)response.StatusCode, errorCode, fbTraceId);
            throw new WhatsAppApiException(errorMessage ?? $"WhatsApp API request '{operation}' failed: {response.StatusCode}", errorCode, fbTraceId)
            {
                ProviderStatusCode = (int)response.StatusCode,
                RetryAfterSeconds = response.Headers.RetryAfter?.Delta is { } delta ? (int)delta.TotalSeconds : null,
            };
        }

        return body;
    }

    public async Task<string?> GetVerifiedNameAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync($"https://graph.facebook.com/{ApiVersion}/{phoneNumberId}?fields=verified_name", cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "WhatsApp API request 'GetVerifiedName' failed to complete");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return doc.RootElement.TryGetProperty("verified_name", out var name) ? name.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task<string?> SendTextMessageAsync(string phoneNumberId, string accessToken, string to, string message, CancellationToken cancellationToken = default)
    {
        return SendAsync(WhatsAppSenderMode.Tenant, accessToken, phoneNumberId, ApiVersion, new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { body = message },
        }, cancellationToken);
    }

    public Task<string?> SendPlatformTextMessageAsync(string to, string message, CancellationToken cancellationToken = default)
    {
        var (accessToken, phoneNumberId, apiVersion) = ResolvePlatformCredentials();
        return SendAsync(WhatsAppSenderMode.Platform, accessToken, phoneNumberId, apiVersion, new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { body = message },
        }, cancellationToken);
    }

    public Task<string?> SendPlatformTemplateMessageAsync(string to, string templateName, string languageCode, CancellationToken cancellationToken = default)
    {
        var (accessToken, phoneNumberId, apiVersion) = ResolvePlatformCredentials();
        return SendAsync(WhatsAppSenderMode.Platform, accessToken, phoneNumberId, apiVersion, new
        {
            messaging_product = "whatsapp",
            to,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
            },
        }, cancellationToken);
    }

    // Resolved lazily on first platform-mode send rather than at startup, so companies that
    // only ever use tenant mode (their own connected WhatsAppAccount) never get blocked by a
    // missing platform credential they don't need.
    private (string AccessToken, string PhoneNumberId, string ApiVersion) ResolvePlatformCredentials()
    {
        var accessToken = configuration["WHATSAPP_ACCESS_TOKEN"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("WHATSAPP_ACCESS_TOKEN is not configured (set it via user secrets or an environment variable).");
        }

        var options = platformOptions.Value;
        if (string.IsNullOrWhiteSpace(options.PhoneNumberId))
        {
            throw new InvalidOperationException("WHATSAPP_PHONE_NUMBER_ID is not configured (set it via user secrets or an environment variable).");
        }

        return (accessToken, options.PhoneNumberId, options.ApiVersion);
    }

    // Single place that actually calls the Graph API - both tenant and platform mode funnel
    // through here so there is exactly one HTTP-call/error-parsing implementation.
    private async Task<string?> SendAsync(WhatsAppSenderMode mode, string accessToken, string phoneNumberId, string apiVersion, object payload, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.PostAsJsonAsync(
                $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages",
                payload,
                cancellationToken);

            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        // Distinguish "our own timeout/network failure" from the caller actually cancelling
        // the request - only the former should degrade to a 502 instead of propagating as
        // cancellation (mirrors the Redis-unreachable fallback pattern).
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "WhatsApp API request failed to complete: mode={Mode}", mode);
            throw new ExternalServiceException("WhatsApp API request timed out or is unreachable") { IsTimeout = true };
        }

        if (!response.IsSuccessStatusCode)
        {
            var (errorMessage, errorCode, fbTraceId) = ParseMetaError(body);

            // Never log accessToken/body verbatim - body can carry the recipient's phone
            // number and other message content in echoed request context.
            logger.LogWarning(
                "WhatsApp API rejected message: mode={Mode} status={StatusCode} code={Code} fbtrace_id={FbTraceId}",
                mode, (int)response.StatusCode, errorCode, fbTraceId);

            throw new WhatsAppApiException(errorMessage ?? $"WhatsApp API rejected the message: {response.StatusCode}", errorCode, fbTraceId)
            {
                ProviderStatusCode = (int)response.StatusCode,
                RetryAfterSeconds = response.Headers.RetryAfter?.Delta is { } delta ? (int)delta.TotalSeconds : null,
            };
        }

        return ParseMessageId(body);
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

    private static string? ParseMessageId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("messages", out var messages) && messages.GetArrayLength() > 0 &&
                messages[0].TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
