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
    private const string TenantApiVersion = "v20.0";

    public Task<WhatsAppOAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        GetOAuthTokenAsync(new Dictionary<string, string>
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
            response = await client.GetAsync($"https://graph.facebook.com/{TenantApiVersion}/oauth/access_token?{query}", cancellationToken);
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
            throw new ExternalServiceException(errorMessage ?? $"WhatsApp API request '{method}' failed: {response.StatusCode}");
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

    public async Task SubscribeWebhookAsync(string accessToken, string wabaId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.PostAsync($"https://graph.facebook.com/{TenantApiVersion}/{wabaId}/subscribed_apps", null, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "WhatsApp API request 'SubscribeWebhook' failed to complete");
            throw new ExternalServiceException("WhatsApp API request timed out or is unreachable");
        }

        if (!response.IsSuccessStatusCode)
        {
            var (errorMessage, errorCode, fbTraceId) = ParseMetaError(body);
            logger.LogWarning("WhatsApp API rejected SubscribeWebhook: status={StatusCode} code={Code} fbtrace_id={FbTraceId}",
                (int)response.StatusCode, errorCode, fbTraceId);
            throw new ExternalServiceException(errorMessage ?? $"WhatsApp API request 'SubscribeWebhook' failed: {response.StatusCode}");
        }
    }

    public async Task<string?> GetVerifiedNameAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync($"https://graph.facebook.com/{TenantApiVersion}/{phoneNumberId}?fields=verified_name", cancellationToken);
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
        return SendAsync(WhatsAppSenderMode.Tenant, accessToken, phoneNumberId, TenantApiVersion, new
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
