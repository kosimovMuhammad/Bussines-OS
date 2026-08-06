using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Contacts;

namespace BusinessOS.Application.Services;

public interface IContactsService
{
    Task<PagedResult<ContactDto>> ListAsync(string companyId, QueryContactsRequest query, CancellationToken cancellationToken = default);
    Task<ContactDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactCommunicationSummary>> GetTimelineAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<ContactDto> CreateAsync(string companyId, CreateContactRequest request, CancellationToken cancellationToken = default);
    Task<ContactDto> UpdateAsync(string companyId, string id, UpdateContactRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
