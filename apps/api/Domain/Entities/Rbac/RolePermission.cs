using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class RolePermission : ITenantScoped
{
    public string RoleId { get; set; } = string.Empty;
    public RbacRole Role { get; set; } = null!;
    public string PermissionId { get; set; } = string.Empty;
    public Permission Permission { get; set; } = null!;
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
