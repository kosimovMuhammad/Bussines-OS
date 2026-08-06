namespace BusinessOS.Domain.Interfaces;

// Implemented by every entity that carries CompanyId, mirroring the Prisma
// tenant-scoping middleware's TENANT_SCOPED_MODELS allowlist. Company itself,
// and the two entities with no direct CompanyId (AiSummary, RefreshToken), don't implement it.
public interface ITenantScoped
{
    string CompanyId { get; set; }
}
