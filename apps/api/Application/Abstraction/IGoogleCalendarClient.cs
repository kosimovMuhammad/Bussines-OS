namespace BusinessOS.Application.Abstraction;

public record GoogleCalendarEvent(string Id, string Title, string? Description, DateTime StartAt, DateTime EndAt, string? Location, bool IsBusinessOsOrigin);

public record GoogleCalendarChanges(IReadOnlyList<GoogleCalendarEvent> Upserted, IReadOnlyList<string> DeletedIds, string? NextSyncToken);

public interface IGoogleCalendarClient
{
    string GetAuthUrl(string state);
    Task<OAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<string> CreateEventAsync(string accessToken, string title, string? description, DateTime startAt, DateTime endAt, string? location, IReadOnlyList<string> attendeeEmails, CancellationToken cancellationToken = default);
    Task PatchEventAsync(string accessToken, string eventId, string title, string? description, DateTime startAt, DateTime endAt, string? location, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(string accessToken, string eventId, CancellationToken cancellationToken = default);
    Task<(string ChannelId, string ResourceId, DateTime Expiration)> WatchCalendarAsync(string accessToken, string channelToken, string webhookUrl, CancellationToken cancellationToken = default);
    Task StopChannelAsync(string accessToken, string channelId, string resourceId, CancellationToken cancellationToken = default);
    Task<GoogleCalendarChanges> ListChangedEventsAsync(string accessToken, string? syncToken, CancellationToken cancellationToken = default);
}
