using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class Department : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }
    public string? ManagerId { get; set; }
    public User? Manager { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Department> SubDepartments { get; set; } = new List<Department>();
}
