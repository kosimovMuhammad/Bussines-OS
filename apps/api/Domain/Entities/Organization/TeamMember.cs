using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// CompanyId is defense-in-depth, same reasoning as RolePermission.CompanyId - not for
// navigation, purely so TenantSaveChangesInterceptor's cross-tenant-mutation guard applies.
public class TeamMember : ITenantScoped
{
    public string TeamId { get; set; } = string.Empty;
    public Team Team { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
