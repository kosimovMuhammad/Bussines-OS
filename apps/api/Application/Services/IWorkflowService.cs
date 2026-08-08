using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Automation;

namespace BusinessOS.Application.Services;

public interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowDto>> ListAsync(string companyId, CancellationToken cancellationToken = default);
    Task<WorkflowDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<WorkflowDetailDto> CreateAsync(string companyId, string actorUserId, CreateWorkflowRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowDetailDto> UpdateAsync(string companyId, string id, UpdateWorkflowRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<WorkflowDetailDto> ToggleAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task<PagedResult<WorkflowExecutionDto>> ListExecutionsAsync(string companyId, string workflowId, QueryWorkflowExecutionsRequest query, CancellationToken cancellationToken = default);
    Task<WorkflowExecutionDetailDto> GetExecutionAsync(string companyId, string workflowId, string executionId, CancellationToken cancellationToken = default);
}
