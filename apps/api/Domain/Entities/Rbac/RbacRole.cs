using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Named RbacRole (not "Role") to avoid colliding with the legacy Domain.Enums.Role
// enum still used by User.Role during the phased RBAC migration - see IMPLEMENTATION_PLAN
// notes in the RBAC work. Every row is scoped to a company: system roles are seeded
// per-company (not shared global templates), so this can be a plain ITenantScoped entity.
public class RbacRole : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
