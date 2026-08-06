using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Infrastructure.Tenancy;

public class CurrentUserContext : ICurrentUserContext
{
    private static readonly AsyncLocal<(string UserId, string CompanyId, Role Role)?> Holder = new();

    public string? UserId => Holder.Value?.UserId;
    public string? CompanyId => Holder.Value?.CompanyId;
    public Role? Role => Holder.Value?.Role;

    public void Set(string userId, string companyId, Role role) => Holder.Value = (userId, companyId, role);
}
