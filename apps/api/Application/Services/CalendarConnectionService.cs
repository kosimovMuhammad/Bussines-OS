using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Calendar;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Application.Services;

public class CalendarConnectionService(
    IAppDbContext db,
    IGoogleCalendarClient googleCalendarClient,
    IOAuthStateService oauthState,
    ITokenCipher tokenCipher,
    IConfiguration configuration) : ICalendarConnectionService
{
    public string GetAuthUrl(string userId, string companyId) =>
        googleCalendarClient.GetAuthUrl(oauthState.Create(userId, companyId));

    public async Task HandleCallbackAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        var payload = oauthState.Validate(state) ?? throw new UnauthorizedAppException("Invalid or expired state");
        var tokens = await googleCalendarClient.ExchangeCodeAsync(code, cancellationToken);

        var connection = await db.OAuthConnections
            .SingleOrDefaultAsync(c => c.UserId == payload.UserId && c.Provider == OAuthProvider.GoogleCalendar, cancellationToken);
        if (connection is null)
        {
            connection = new OAuthConnection { CompanyId = payload.CompanyId, UserId = payload.UserId, Provider = OAuthProvider.GoogleCalendar };
            db.OAuthConnections.Add(connection);
        }

        connection.EncryptedAccessToken = tokenCipher.Encrypt(tokens.AccessToken);
        connection.EncryptedRefreshToken = tokens.RefreshToken is null ? connection.EncryptedRefreshToken : tokenCipher.Encrypt(tokens.RefreshToken);
        connection.ExpiresAt = tokens.ExpiresAt;
        await db.SaveChangesAsync(cancellationToken);

        var channelToken = Guid.NewGuid().ToString();
        var webhookUrl = $"{configuration["PUBLIC_URL"]?.TrimEnd('/')}/webhooks/google-calendar";
        var (channelId, resourceId, expiration) = await googleCalendarClient.WatchCalendarAsync(tokens.AccessToken, channelToken, webhookUrl, cancellationToken);

        var syncToken = await db.CalendarSyncTokens.SingleOrDefaultAsync(t => t.UserId == payload.UserId, cancellationToken);
        if (syncToken is null)
        {
            syncToken = new CalendarSyncToken { CompanyId = payload.CompanyId, UserId = payload.UserId };
            db.CalendarSyncTokens.Add(syncToken);
        }

        syncToken.ResourceId = resourceId;
        syncToken.ChannelId = channelId;
        syncToken.ChannelToken = channelToken;
        syncToken.ChannelExpiration = expiration;
        syncToken.SyncToken = null;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CalendarStatusDto> GetStatusAsync(string companyId, string userId, CancellationToken cancellationToken = default)
    {
        var token = await db.CalendarSyncTokens.SingleOrDefaultAsync(t => t.UserId == userId, cancellationToken);
        return new CalendarStatusDto(token is not null, token?.ChannelExpiration);
    }

    public async Task DisconnectAsync(string companyId, string userId, CancellationToken cancellationToken = default)
    {
        var connection = await db.OAuthConnections.SingleOrDefaultAsync(c => c.UserId == userId && c.Provider == OAuthProvider.GoogleCalendar, cancellationToken);
        var syncToken = await db.CalendarSyncTokens.SingleOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (connection is not null && syncToken is not null)
        {
            try
            {
                var accessToken = tokenCipher.Decrypt(connection.EncryptedAccessToken);
                await googleCalendarClient.StopChannelAsync(accessToken, syncToken.ChannelId, syncToken.ResourceId, cancellationToken);
            }
            catch
            {
                // Best-effort - still clear local state below even if Google rejects the stop call.
            }
        }

        if (connection is not null) db.OAuthConnections.Remove(connection);
        if (syncToken is not null) db.CalendarSyncTokens.Remove(syncToken);

        var meetings = await db.Meetings.Where(m => m.OrganizerId == userId && m.GoogleEventId != null).ToListAsync(cancellationToken);
        foreach (var meeting in meetings)
        {
            meeting.SyncStatus = MeetingSyncStatus.NotSynced;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> VerifyChannelAsync(string channelId, string channelToken, CancellationToken cancellationToken = default)
    {
        return await db.CalendarSyncTokens.AnyAsync(t => t.ChannelId == channelId && t.ChannelToken == channelToken, cancellationToken);
    }

    public async Task ProcessInboundChangesByChannelAsync(string channelId, CancellationToken cancellationToken = default)
    {
        var syncToken = await db.CalendarSyncTokens.SingleOrDefaultAsync(t => t.ChannelId == channelId, cancellationToken);
        if (syncToken is null) return;

        var connection = await db.OAuthConnections
            .SingleOrDefaultAsync(c => c.UserId == syncToken.UserId && c.Provider == OAuthProvider.GoogleCalendar, cancellationToken);
        if (connection is null) return;

        var accessToken = tokenCipher.Decrypt(connection.EncryptedAccessToken);
        var changes = await googleCalendarClient.ListChangedEventsAsync(accessToken, syncToken.SyncToken, cancellationToken);

        if (changes.NextSyncToken is null && syncToken.SyncToken is not null)
        {
            // 410 Gone - the stored token expired; reset to a full sync next time.
            syncToken.SyncToken = null;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        foreach (var deletedId in changes.DeletedIds)
        {
            var meeting = await db.Meetings.SingleOrDefaultAsync(m => m.GoogleEventId == deletedId, cancellationToken);
            if (meeting is not null)
            {
                meeting.Status = MeetingStatus.Cancelled;
            }
        }

        foreach (var evt in changes.Upserted)
        {
            if (evt.IsBusinessOsOrigin)
            {
                continue;
            }

            var meeting = await db.Meetings.SingleOrDefaultAsync(m => m.GoogleEventId == evt.Id, cancellationToken);
            if (meeting is null)
            {
                meeting = new Meeting
                {
                    CompanyId = syncToken.CompanyId,
                    OrganizerId = syncToken.UserId,
                    GoogleEventId = evt.Id,
                    AttendeeUserIds = [syncToken.UserId],
                };
                db.Meetings.Add(meeting);
            }

            meeting.Title = evt.Title;
            meeting.Description = evt.Description;
            meeting.StartAt = evt.StartAt;
            meeting.EndAt = evt.EndAt;
            meeting.Location = evt.Location;
            meeting.SyncStatus = MeetingSyncStatus.Synced;
            meeting.LastSyncedAt = DateTime.UtcNow;
        }

        syncToken.SyncToken = changes.NextSyncToken ?? syncToken.SyncToken;
        await db.SaveChangesAsync(cancellationToken);
    }
}
