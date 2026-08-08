using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Activity;
using BusinessOS.Application.ViewModels.Contacts;

namespace BusinessOS.Application.Services;

public interface IContactsService
{
    Task<PagedResult<ContactDto>> ListAsync(string companyId, QueryContactsRequest query, CancellationToken cancellationToken = default);
    Task<ContactDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);

    // Phase 6: upgraded from a Communications-only feed to a merged ActivityLog +
    // Communications timeline (see IActivityLogService.GetContactTimelineAsync) - same
    // route, richer payload.
    Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task<ContactDto> CreateAsync(string companyId, CreateContactRequest request, CancellationToken cancellationToken = default);
    Task<ContactDto> UpdateAsync(string companyId, string id, UpdateContactRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task<BulkOperationResult> BulkUpdateAsync(string companyId, IReadOnlyList<string> ids, UpdateContactRequest patch, CancellationToken cancellationToken = default);
    Task<BulkOperationResult> BulkDeleteAsync(string companyId, IReadOnlyList<string> ids, CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(string companyId, CancellationToken cancellationToken = default);
    Task<ImportResultDto> ImportCsvAsync(string companyId, string csvContent, CancellationToken cancellationToken = default);
}
