using BusinessOS.Application.ViewModels.Calendar;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface ICalendarService
{
    Task<IReadOnlyList<MeetingDto>> ListAsync(string companyId, string actorUserId, Role actorRole, QueryMeetingsRequest query, CancellationToken cancellationToken = default);
    Task<MeetingDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<MeetingDto> CreateAsync(string companyId, string organizerId, CreateMeetingRequest request, CancellationToken cancellationToken = default);
    Task<MeetingDto> UpdateAsync(string companyId, string id, UpdateMeetingRequest request, CancellationToken cancellationToken = default);
    Task<MeetingDto> UpdateStatusAsync(string companyId, string id, UpdateMeetingStatusRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);

    // Background-queue entry points (resolved fresh from a scoped IServiceProvider).
    Task SyncOutboundUpsertAsync(string meetingId, bool isCreate, CancellationToken cancellationToken = default);
    Task SyncOutboundDeleteAsync(string organizerId, string googleEventId, CancellationToken cancellationToken = default);
}
