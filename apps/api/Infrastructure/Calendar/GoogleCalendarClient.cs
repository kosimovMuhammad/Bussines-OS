using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;

namespace BusinessOS.Infrastructure.Calendar;

public class GoogleCalendarClient(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IGoogleCalendarClient
{
    private const string Scope = "https://www.googleapis.com/auth/calendar.events";
    private const string EventsUrl = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

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

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("Empty response from Google token endpoint");

        return new OAuthTokenResult(payload.AccessToken, payload.RefreshToken, DateTime.UtcNow.AddSeconds(payload.ExpiresIn));
    }

    public async Task<string> CreateEventAsync(string accessToken, string title, string? description, DateTime startAt, DateTime endAt, string? location, IReadOnlyList<string> attendeeEmails, CancellationToken cancellationToken = default)
    {
        var client = AuthedClient(accessToken);
        var response = await client.PostAsJsonAsync(EventsUrl, BuildEventBody(title, description, startAt, endAt, location, attendeeEmails), cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return created.GetProperty("id").GetString()!;
    }

    public async Task PatchEventAsync(string accessToken, string eventId, string title, string? description, DateTime startAt, DateTime endAt, string? location, CancellationToken cancellationToken = default)
    {
        var client = AuthedClient(accessToken);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{EventsUrl}/{eventId}")
        {
            Content = JsonContent.Create(BuildEventBody(title, description, startAt, endAt, location, [])),
        };
        var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
    }

    public async Task DeleteEventAsync(string accessToken, string eventId, CancellationToken cancellationToken = default)
    {
        var client = AuthedClient(accessToken);
        var response = await client.DeleteAsync($"{EventsUrl}/{eventId}", cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccess(response, cancellationToken);
        }
    }

    public async Task<(string ChannelId, string ResourceId, DateTime Expiration)> WatchCalendarAsync(string accessToken, string channelToken, string webhookUrl, CancellationToken cancellationToken = default)
    {
        var channelId = Guid.NewGuid().ToString();
        var client = AuthedClient(accessToken);
        var response = await client.PostAsJsonAsync($"{EventsUrl}/watch", new
        {
            id = channelId,
            type = "web_hook",
            address = webhookUrl,
            token = channelToken,
        }, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var resourceId = body.GetProperty("resourceId").GetString()!;
        var expirationMs = long.Parse(body.GetProperty("expiration").GetString()!);
        return (channelId, resourceId, DateTimeOffset.FromUnixTimeMilliseconds(expirationMs).UtcDateTime);
    }

    public async Task StopChannelAsync(string accessToken, string channelId, string resourceId, CancellationToken cancellationToken = default)
    {
        var client = AuthedClient(accessToken);
        var response = await client.PostAsJsonAsync("https://www.googleapis.com/calendar/v3/channels/stop", new
        {
            id = channelId,
            resourceId,
        }, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
    }

    public async Task<GoogleCalendarChanges> ListChangedEventsAsync(string accessToken, string? syncToken, CancellationToken cancellationToken = default)
    {
        var client = AuthedClient(accessToken);
        var url = string.IsNullOrEmpty(syncToken)
            ? $"{EventsUrl}?maxResults=250"
            : $"{EventsUrl}?syncToken={Uri.EscapeDataString(syncToken)}";

        var response = await client.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Gone)
        {
            // syncToken expired - caller should reset to a full sync (null token) and retry.
            return new GoogleCalendarChanges([], [], null);
        }

        await EnsureSuccess(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var upserted = new List<GoogleCalendarEvent>();
        var deleted = new List<string>();

        if (body.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString()!;
                var status = item.TryGetProperty("status", out var s) ? s.GetString() : null;
                if (status == "cancelled")
                {
                    deleted.Add(id);
                    continue;
                }

                var isBusinessOsOrigin = item.TryGetProperty("extendedProperties", out var ext) &&
                    ext.TryGetProperty("private", out var priv) &&
                    priv.TryGetProperty("source", out var src) &&
                    src.GetString() == "business-os";

                var start = ParseEventTime(item, "start");
                var end = ParseEventTime(item, "end");
                if (start is null || end is null)
                {
                    continue;
                }

                upserted.Add(new GoogleCalendarEvent(
                    id,
                    item.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "(no title)" : "(no title)",
                    item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    start.Value,
                    end.Value,
                    item.TryGetProperty("location", out var loc) ? loc.GetString() : null,
                    isBusinessOsOrigin));
            }
        }

        var nextSyncToken = body.TryGetProperty("nextSyncToken", out var nst) ? nst.GetString() : null;
        return new GoogleCalendarChanges(upserted, deleted, nextSyncToken);
    }

    private static DateTime? ParseEventTime(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var timeObj))
        {
            return null;
        }

        if (timeObj.TryGetProperty("dateTime", out var dt) && DateTime.TryParse(dt.GetString(), out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        if (timeObj.TryGetProperty("date", out var d) && DateTime.TryParse(d.GetString(), out var dateOnly))
        {
            return dateOnly;
        }

        return null;
    }

    private static object BuildEventBody(string title, string? description, DateTime startAt, DateTime endAt, string? location, IReadOnlyList<string> attendeeEmails) => new
    {
        summary = title,
        description,
        location,
        start = new { dateTime = startAt.ToString("o") },
        end = new { dateTime = endAt.ToString("o") },
        attendees = attendeeEmails.Select(email => new { email }).ToArray(),
        extendedProperties = new { @private = new { source = "business-os" } },
    };

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Google Calendar API error: {response.StatusCode} {body}");
        }
    }

    private HttpClient AuthedClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private string RedirectUri() => $"{configuration["PUBLIC_URL"]?.TrimEnd('/')}/calendar/connect/google/callback";

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
