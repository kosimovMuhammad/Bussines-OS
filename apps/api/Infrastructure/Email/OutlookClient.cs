using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Email;

public class OutlookClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OutlookClient> logger) : IOutlookClient
{
    private const string Scope = "offline_access Mail.Read Mail.Send";

    public string GetAuthUrl(string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = configuration["MICROSOFT_CLIENT_ID"];
        query["response_type"] = "code";
        query["redirect_uri"] = RedirectUri();
        query["scope"] = Scope;
        query["state"] = state;
        return $"https://login.microsoftonline.com/{TenantId()}/oauth2/v2.0/authorize?{query}";
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsync($"https://login.microsoftonline.com/{TenantId()}/oauth2/v2.0/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = configuration["MICROSOFT_CLIENT_ID"] ?? string.Empty,
            ["client_secret"] = configuration["MICROSOFT_CLIENT_SECRET"] ?? string.Empty,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri(),
            ["scope"] = Scope,
        }), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Microsoft token exchange failed: {response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<MicrosoftTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("Empty response from Microsoft token endpoint");

        return new OAuthTokenResult(payload.AccessToken, payload.RefreshToken, DateTime.UtcNow.AddSeconds(payload.ExpiresIn));
    }

    // Graph's sendMail returns 202 Accepted with an empty body on success - unlike Gmail's
    // messages.send, there is no message id to hand back.
    public async Task<string?> SendEmailAsync(string accessToken, string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("https://graph.microsoft.com/v1.0/me/sendMail", new
            {
                message = new
                {
                    subject,
                    body = new { contentType = "Text", content = body },
                    toRecipients = new[] { new { emailAddress = new { address = toEmail } } },
                },
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Outlook API request 'SendEmail' failed to complete");
            throw new ExternalServiceException("Outlook API request timed out or is unreachable") { IsTimeout = true };
        }

        if (!response.IsSuccessStatusCode)
        {
            // Never log toEmail/accessToken/body verbatim - body echoes recipient/message context.
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorMessage = ParseGraphError(errorBody);
            logger.LogWarning("Outlook API rejected SendEmail: status={StatusCode}", (int)response.StatusCode);

            throw new ExternalServiceException(errorMessage ?? $"Outlook API rejected the message: {response.StatusCode}")
            {
                ProviderStatusCode = (int)response.StatusCode,
                RetryAfterSeconds = response.Headers.RetryAfter?.Delta is { } delta ? (int)delta.TotalSeconds : null,
            };
        }

        return null;
    }

    private static string? ParseGraphError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var m)
                ? m.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string RedirectUri() => configuration["MICROSOFT_REDIRECT_URI"] ?? $"{configuration["PUBLIC_URL"]?.TrimEnd('/')}/email/connect/outlook/callback";

    private string TenantId() => configuration["MICROSOFT_TENANT_ID"] ?? "common";

    private class MicrosoftTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
