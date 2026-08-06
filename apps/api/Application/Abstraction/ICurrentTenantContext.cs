namespace BusinessOS.Application.Abstraction;

// AsyncLocal-backed, populated by tenant middleware right after JWT auth succeeds
// (equivalent to the old TenantGuard). Read by EF Core global query filters and
// the SaveChanges interceptor, and consumed directly by services as an explicit
// defense-in-depth companyId source.
public interface ICurrentTenantContext
{
    string? CompanyId { get; }
    void Set(string companyId);
}
