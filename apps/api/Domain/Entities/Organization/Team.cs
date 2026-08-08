using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class Team : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TeamLeadId { get; set; }
    public User? TeamLead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
