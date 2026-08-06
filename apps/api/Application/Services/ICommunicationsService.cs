using BusinessOS.Application.ViewModels.Communications;

namespace BusinessOS.Application.Services;

public interface ICommunicationsService
{
    Task<IReadOnlyList<CommunicationDto>> ListAsync(string companyId, QueryCommunicationsRequest query, CancellationToken cancellationToken = default);
    Task<CommunicationDto> CreateNoteAsync(string companyId, CreateNoteRequest request, CancellationToken cancellationToken = default);
}
