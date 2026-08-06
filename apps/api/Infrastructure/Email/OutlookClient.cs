using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;

namespace BusinessOS.Infrastructure.Email;

public class OutlookClient(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IOutlookClient
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

    public async Task SendEmailAsync(string accessToken, string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("https://graph.microsoft.com/v1.0/me/sendMail", new
        {
            message = new
            {
                subject,
                body = new { contentType = "Text", content = body },
                toRecipients = new[] { new { emailAddress = new { address = toEmail } } },
            },
        }, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Outlook send failed: {response.StatusCode} {errorBody}");
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
