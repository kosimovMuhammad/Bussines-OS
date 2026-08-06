using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class Invoice : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? DealId { get; set; }
    public Deal? Deal { get; set; }
    public string ContactId { get; set; } = string.Empty;
    public Contact Contact { get; set; } = null!;
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TJS";
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<FileAsset> Files { get; set; } = new List<FileAsset>();
}
