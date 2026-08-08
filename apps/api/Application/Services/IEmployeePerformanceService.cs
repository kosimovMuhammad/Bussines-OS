using BusinessOS.Application.ViewModels.Reports;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface IEmployeePerformanceService
{
    // targetUserId == null -> company-wide view, requires requestingUserRole to be
    // Owner/Admin. targetUserId == requestingUserId -> self view, always allowed.
    // targetUserId pointing at someone else -> requires Owner/Admin too. Enforced here
    // (row-level), not at the controller, since RolePermissions' current matrix grants
    // Read on every module to every role.
    Task<IReadOnlyList<EmployeePerformanceDto>> GetAsync(
        string companyId, string requestingUserId, Role requestingUserRole, string? targetUserId,
        DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
