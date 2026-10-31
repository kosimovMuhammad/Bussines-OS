using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Abstraction;

public interface ICurrentTenantContext
{
    string? CompanyId { get; }
    Role? Role { get; }
    string? BranchId { get; }
    void Set(string companyId, Role role, string? branchId);
}
