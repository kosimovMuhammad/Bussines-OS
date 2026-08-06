using BusinessOS.Application.ViewModels.Tasks;

namespace BusinessOS.Application.Services;

public interface ITimeEntriesService
{
    Task<TimeEntryDto?> GetActiveAsync(string companyId, string userId, CancellationToken cancellationToken = default);
    Task<TimeEntryDto> StartAsync(string companyId, string userId, string taskId, StartTimeRequest request, CancellationToken cancellationToken = default);
    Task<TimeEntryDto> StopAsync(string companyId, string userId, string taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeEntryDto>> ListForTaskAsync(string companyId, string taskId, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string taskId, string entryId, CancellationToken cancellationToken = default);
}
