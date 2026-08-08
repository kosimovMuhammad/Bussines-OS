namespace BusinessOS.Domain.Entities;

// System-defined catalog, not tenant scoped - every tenant sees the same set of
// permissions and can only choose which ones each of its roles is granted.
public class Permission
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
