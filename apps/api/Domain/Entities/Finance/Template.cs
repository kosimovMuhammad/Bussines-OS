using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

// Content uses the same "{{fieldName}}" placeholder syntax as
// Application.Services.TemplateResolver (already used for Automation Engine action
// configs) - reused here rather than inventing a second syntax, see ContractsService's use
// of TemplateResolver.Resolve when a Contract is created from a Template.
public class Template : IActivityLoggable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public TemplateType Type { get; set; } = TemplateType.Contract;
    public string Content { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
