using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Per-user exception layered on top of the role grant: Allow=true grants a permission the
// user's role doesn't have, Allow=false revokes a permission the role does have. Absence of
// a row means "defer to the role" - see PermissionAuthorizationFilter for evaluation order.
public class UserPermissionOverride : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public string PermissionId { get; set; } = string.Empty;
    public Permission Permission { get; set; } = null!;
    public bool Allow { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
