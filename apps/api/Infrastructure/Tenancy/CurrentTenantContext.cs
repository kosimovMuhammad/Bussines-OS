using BusinessOS.Application.Abstraction;

namespace BusinessOS.Infrastructure.Tenancy;

public class CurrentTenantContext : ICurrentTenantContext
{
    private static readonly AsyncLocal<string?> Holder = new();

    public string? CompanyId => Holder.Value;

    public void Set(string companyId) => Holder.Value = companyId;
}
