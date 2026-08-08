using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Activity;
using BusinessOS.Application.ViewModels.Tasks;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface ITasksService
{
    Task<IReadOnlyList<TaskDto>> ListAsync(string companyId, string actorUserId, Role actorRole, QueryTasksRequest query, CancellationToken cancellationToken = default);
    Task<TaskDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimelineEventDto>> GetTimelineAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<TaskDto> CreateAsync(string companyId, CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateAsync(string companyId, string actorUserId, Role actorRole, string id, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateStatusAsync(string companyId, string id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task<BulkOperationResult> BulkUpdateAsync(string companyId, string actorUserId, Role actorRole, IReadOnlyList<string> ids, UpdateTaskRequest patch, CancellationToken cancellationToken = default);
    Task<BulkOperationResult> BulkDeleteAsync(string companyId, IReadOnlyList<string> ids, CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(string companyId, CancellationToken cancellationToken = default);
    Task<ImportResultDto> ImportCsvAsync(string companyId, string csvContent, CancellationToken cancellationToken = default);
}
