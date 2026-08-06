using BusinessOS.Application.ViewModels.Tasks;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface ITasksService
{
    Task<IReadOnlyList<TaskDto>> ListAsync(string companyId, string actorUserId, Role actorRole, QueryTasksRequest query, CancellationToken cancellationToken = default);
    Task<TaskDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<TaskDto> CreateAsync(string companyId, CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateAsync(string companyId, string actorUserId, Role actorRole, string id, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateStatusAsync(string companyId, string id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
