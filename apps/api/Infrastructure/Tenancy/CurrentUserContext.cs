using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Infrastructure.Tenancy;

public class CurrentUserContext : ICurrentUserContext
{
    private static readonly AsyncLocal<(string UserId, string CompanyId, Role Role, string? BranchId)?> Holder = new();

    public string? UserId => Holder.Value?.UserId;
    public string? CompanyId => Holder.Value?.CompanyId;
    public Role? Role => Holder.Value?.Role;
    public string? BranchId => Holder.Value?.BranchId;

    public void Set(string userId, string companyId, Role role, string? branchId) => Holder.Value = (userId, companyId, role, branchId);
}
