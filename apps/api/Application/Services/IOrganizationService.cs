using BusinessOS.Application.ViewModels.Organization;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface IOrganizationService
{
    Task UpdateAssignmentAsync(string companyId, string actorUserId, Role actorRole, string targetUserId, UpdateAssignmentRequest request, CancellationToken cancellationToken = default);

    Task<OrgChartResponse> GetOrgChartAsync(string companyId, CancellationToken cancellationToken = default);
    Task<UserOrgChainDto> GetUserOrgChainAsync(string companyId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepartmentDto>> ListDepartmentsFlatAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DepartmentTreeNode>> ListDepartmentsTreeAsync(string companyId, CancellationToken cancellationToken = default);
    Task<DepartmentDto> GetDepartmentAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<DepartmentDto> CreateDepartmentAsync(string companyId, CreateDepartmentRequest request, CancellationToken cancellationToken = default);
    Task<DepartmentDto> UpdateDepartmentAsync(string companyId, string id, UpdateDepartmentRequest request, CancellationToken cancellationToken = default);
    Task RemoveDepartmentAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamDto>> ListTeamsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<TeamDto> GetTeamAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<TeamDto> CreateTeamAsync(string companyId, CreateTeamRequest request, CancellationToken cancellationToken = default);
    Task<TeamDto> UpdateTeamAsync(string companyId, string id, UpdateTeamRequest request, CancellationToken cancellationToken = default);
    Task RemoveTeamAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamMemberDto>> ListTeamMembersAsync(string companyId, string teamId, CancellationToken cancellationToken = default);
    Task AddTeamMemberAsync(string companyId, string teamId, AddTeamMemberRequest request, CancellationToken cancellationToken = default);
    Task RemoveTeamMemberAsync(string companyId, string teamId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchDto>> ListBranchesAsync(string companyId, CancellationToken cancellationToken = default);
    Task<BranchDto> GetBranchAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<BranchDto> CreateBranchAsync(string companyId, CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDto> UpdateBranchAsync(string companyId, string id, UpdateBranchRequest request, CancellationToken cancellationToken = default);
    Task RemoveBranchAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
