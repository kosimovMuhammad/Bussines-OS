using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;
using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;

namespace BusinessOS.Infrastructure.Email;

public class GmailClient(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IGmailClient
{
    private const string Scope = "https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/gmail.send https://www.googleapis.com/auth/gmail.modify";

    public string GetAuthUrl(string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = configuration["GOOGLE_CLIENT_ID"];
        query["redirect_uri"] = RedirectUri();
        query["response_type"] = "code";
        query["scope"] = Scope;
        query["access_type"] = "offline";
        query["prompt"] = "consent";
        query["state"] = state;
        return $"https://accounts.google.com/o/oauth2/v2/auth?{query}";
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = configuration["GOOGLE_CLIENT_ID"] ?? string.Empty,
            ["client_secret"] = configuration["GOOGLE_CLIENT_SECRET"] ?? string.Empty,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri(),
        }), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Google token exchange failed: {response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("Empty response from Google token endpoint");

        return new OAuthTokenResult(payload.AccessToken, payload.RefreshToken, DateTime.UtcNow.AddSeconds(payload.ExpiresIn));
    }

    public async Task SendEmailAsync(string accessToken, string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var mime = $"To: {toEmail}\r\nSubject: {subject}\r\nContent-Type: text/plain; charset=UTF-8\r\n\r\n{body}";
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages/send",
            new { raw },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Gmail send failed: {response.StatusCode} {errorBody}");
        }
    }

    private string RedirectUri() => $"{configuration["PUBLIC_URL"]?.TrimEnd('/')}/email/connect/gmail/callback";

    private class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
