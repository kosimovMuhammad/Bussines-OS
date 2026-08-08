using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Flat record, deliberately no approval status - no approval-workflow pattern exists
// anywhere else in this codebase (checked before adding one here), so Pending/Approved/
// Rejected would be invented, not reused. If approvals are ever needed, they belong in the
// Automation Engine (Workflow triggers/actions already model exactly this kind of
// state-gated process) rather than a bespoke field on Expense.
public class Expense : IActivityLoggable, IArchivable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TJS";
    public string Category { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string PaidById { get; set; } = string.Empty;
    public User PaidBy { get; set; } = null!;
    public string? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string? ReceiptFileId { get; set; }
    public FileAsset? ReceiptFile { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
