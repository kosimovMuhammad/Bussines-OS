using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Calendar;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Application.Services;

public class CalendarService(IAppDbContext db, IBackgroundTaskQueue queue, IGoogleCalendarClient googleCalendarClient, ITokenCipher tokenCipher) : ICalendarService
{
    public async Task<IReadOnlyList<MeetingDto>> ListAsync(string companyId, string actorUserId, Role actorRole, QueryMeetingsRequest query, CancellationToken cancellationToken = default)
    {
        var meetings = db.Meetings.Include(m => m.Contact).Include(m => m.Deal).Include(m => m.Organizer).AsQueryable();

        if (actorRole == Role.Employee)
        {
            meetings = meetings.Where(m => m.OrganizerId == actorUserId || m.AttendeeUserIds.Contains(actorUserId));
        }

        if (query.From is not null) meetings = meetings.Where(m => m.StartAt >= query.From);
        if (query.To is not null) meetings = meetings.Where(m => m.EndAt <= query.To);
        if (!string.IsNullOrWhiteSpace(query.ContactId)) meetings = meetings.Where(m => m.ContactId == query.ContactId);
        if (!string.IsNullOrWhiteSpace(query.DealId)) meetings = meetings.Where(m => m.DealId == query.DealId);
        if (!string.IsNullOrWhiteSpace(query.OrganizerId)) meetings = meetings.Where(m => m.OrganizerId == query.OrganizerId);

        var items = await meetings.OrderBy(m => m.StartAt).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<MeetingDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var meeting = await db.Meetings.Include(m => m.Contact).Include(m => m.Deal).Include(m => m.Organizer)
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException("Meeting not found");
        return ToDto(meeting);
    }

    public async Task<MeetingDto> CreateAsync(string companyId, string organizerId, CreateMeetingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ContactId is not null && !await db.Contacts.AnyAsync(c => c.Id == request.ContactId, cancellationToken))
        {
            throw new NotFoundException("Contact not found");
        }
        if (request.DealId is not null && !await db.Deals.AnyAsync(d => d.Id == request.DealId, cancellationToken))
        {
            throw new NotFoundException("Deal not found");
        }

        var meeting = new Meeting
        {
            CompanyId = companyId,
            Title = request.Title,
            ContactId = request.ContactId,
            DealId = request.DealId,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Location = request.Location,
            Description = request.Description,
            OrganizerId = organizerId,
            AttendeeUserIds = request.AttendeeUserIds ?? [organizerId],
        };

        db.Meetings.Add(meeting);
        await db.SaveChangesAsync(cancellationToken);

        await EnqueueUpsert(meeting.Id, isCreate: true);

        return await GetAsync(companyId, meeting.Id, cancellationToken);
    }

    public async Task<MeetingDto> UpdateAsync(string companyId, string id, UpdateMeetingRequest request, CancellationToken cancellationToken = default)
    {
        var meeting = await db.Meetings.SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException("Meeting not found");

        if (request.ContactId is not null)
        {
            if (!await db.Contacts.AnyAsync(c => c.Id == request.ContactId, cancellationToken)) throw new NotFoundException("Contact not found");
            meeting.ContactId = request.ContactId;
        }
        if (request.DealId is not null)
        {
            if (!await db.Deals.AnyAsync(d => d.Id == request.DealId, cancellationToken)) throw new NotFoundException("Deal not found");
            meeting.DealId = request.DealId;
        }

        if (request.Title is not null) meeting.Title = request.Title;
        if (request.StartAt is not null) meeting.StartAt = request.StartAt.Value;
        if (request.EndAt is not null) meeting.EndAt = request.EndAt.Value;
        if (request.Location is not null) meeting.Location = request.Location;
        if (request.Description is not null) meeting.Description = request.Description;
        if (request.AttendeeUserIds is not null) meeting.AttendeeUserIds = request.AttendeeUserIds;

        await db.SaveChangesAsync(cancellationToken);

        await EnqueueUpsert(meeting.Id, isCreate: meeting.GoogleEventId is null);

        return await GetAsync(companyId, meeting.Id, cancellationToken);
    }

    public async Task<MeetingDto> UpdateStatusAsync(string companyId, string id, UpdateMeetingStatusRequest request, CancellationToken cancellationToken = default)
    {
        var meeting = await db.Meetings.SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException("Meeting not found");

        meeting.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);

        if (meeting.GoogleEventId is not null)
        {
            await EnqueueUpsert(meeting.Id, isCreate: false);
        }

        return await GetAsync(companyId, meeting.Id, cancellationToken);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var meeting = await db.Meetings.SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException("Meeting not found");

        db.Meetings.Remove(meeting);
        await db.SaveChangesAsync(cancellationToken);

        if (meeting.GoogleEventId is not null)
        {
            var organizerId = meeting.OrganizerId;
            var googleEventId = meeting.GoogleEventId;
            await queue.QueueAsync(async (sp, ct) =>
            {
                var service = sp.GetRequiredService<ICalendarService>();
                await service.SyncOutboundDeleteAsync(organizerId, googleEventId, ct);
            });
        }
    }

    public async Task SyncOutboundUpsertAsync(string meetingId, bool isCreate, CancellationToken cancellationToken = default)
    {
        var meeting = await db.Meetings.SingleOrDefaultAsync(m => m.Id == meetingId, cancellationToken);
        if (meeting is null) return;

        var connection = await db.OAuthConnections
            .SingleOrDefaultAsync(c => c.UserId == meeting.OrganizerId && c.Provider == OAuthProvider.GoogleCalendar, cancellationToken);
        if (connection is null) return;

        var accessToken = tokenCipher.Decrypt(connection.EncryptedAccessToken);
        var attendeeEmails = await db.Users
            .Where(u => meeting.AttendeeUserIds.Contains(u.Id))
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);

        try
        {
            if (isCreate || meeting.GoogleEventId is null)
            {
                var eventId = await googleCalendarClient.CreateEventAsync(
                    accessToken, meeting.Title, meeting.Description, meeting.StartAt, meeting.EndAt, meeting.Location, attendeeEmails, cancellationToken);
                meeting.GoogleEventId = eventId;
            }
            else
            {
                await googleCalendarClient.PatchEventAsync(
                    accessToken, meeting.GoogleEventId, meeting.Title, meeting.Description, meeting.StartAt, meeting.EndAt, meeting.Location, cancellationToken);
            }

            meeting.SyncStatus = MeetingSyncStatus.Synced;
            meeting.SyncError = null;
            meeting.LastSyncedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            meeting.SyncStatus = MeetingSyncStatus.SyncFailed;
            meeting.SyncError = ex.Message;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncOutboundDeleteAsync(string organizerId, string googleEventId, CancellationToken cancellationToken = default)
    {
        var connection = await db.OAuthConnections
            .SingleOrDefaultAsync(c => c.UserId == organizerId && c.Provider == OAuthProvider.GoogleCalendar, cancellationToken);
        if (connection is null) return;

        var accessToken = tokenCipher.Decrypt(connection.EncryptedAccessToken);
        await googleCalendarClient.DeleteEventAsync(accessToken, googleEventId, cancellationToken);
    }

    private async Task EnqueueUpsert(string meetingId, bool isCreate)
    {
        await queue.QueueAsync(async (sp, ct) =>
        {
            var service = sp.GetRequiredService<ICalendarService>();
            await service.SyncOutboundUpsertAsync(meetingId, isCreate, ct);
        });
    }

    private static MeetingDto ToDto(Meeting m) => new(
        m.Id, m.CompanyId, m.ContactId,
        m.Contact is null ? null : $"{m.Contact.FirstName} {m.Contact.LastName}".Trim(),
        m.DealId, m.Deal?.Title, m.Title, m.Description, m.StartAt, m.EndAt, m.Location,
        m.OrganizerId, m.Organizer.Name, m.AttendeeUserIds, m.Status.ToString(), m.SyncStatus.ToString(), m.CreatedAt, m.UpdatedAt);
}
