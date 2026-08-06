using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class Deal : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string ContactId { get; set; } = string.Empty;
    public Contact Contact { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Currency { get; set; } = "TJS";
    public DealStage Stage { get; set; } = DealStage.Lead;
    public int Probability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public string? OwnerId { get; set; }
    public User? Owner { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<FileAsset> Files { get; set; } = new List<FileAsset>();
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
    public ICollection<AiQuotation> AiQuotations { get; set; } = new List<AiQuotation>();
}
