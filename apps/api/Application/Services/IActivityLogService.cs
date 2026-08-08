using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Activity;

namespace BusinessOS.Application.Services;

public interface IActivityLogService
{
    Task<PagedResult<ActivityLogEntryDto>> ListAsync(string companyId, QueryActivityLogRequest query, CancellationToken cancellationToken = default);
    Task<PagedResult<ActivityLogEntryDto>> GetForUserAsync(string companyId, string userId, QueryActivityLogRequest query, CancellationToken cancellationToken = default);
    Task<PagedResult<ActivityLogEntryDto>> GetForEntityAsync(string companyId, string entityType, string entityId, QueryActivityLogRequest query, CancellationToken cancellationToken = default);

    // Phase 6 timelines: merged ActivityLog + Communication view for one entity plus its
    // directly-related entities (a Deal's timeline also carries its Contact's and Tasks'
    // activity, etc.) - not paginated, since it's bounded to one entity's own neighborhood
    // rather than a company-wide feed. Company-wide "timeline" is just ListAsync itself, no
    // new method needed for that one.
    Task<IReadOnlyList<TimelineEventDto>> GetContactTimelineAsync(string companyId, string contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimelineEventDto>> GetDealTimelineAsync(string companyId, string dealId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimelineEventDto>> GetTaskTimelineAsync(string companyId, string taskId, CancellationToken cancellationToken = default);
}
