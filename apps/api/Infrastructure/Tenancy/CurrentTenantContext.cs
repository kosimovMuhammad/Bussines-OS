using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Infrastructure.Tenancy;

public class CurrentTenantContext : ICurrentTenantContext
{
    private static readonly AsyncLocal<(string CompanyId, Role Role, string? BranchId)?> Holder = new();

    public string? CompanyId => Holder.Value?.CompanyId;
    public Role? Role => Holder.Value?.Role;
    public string? BranchId => Holder.Value?.BranchId;

    public void Set(string companyId, Role role, string? branchId) => Holder.Value = (companyId, role, branchId);
}
