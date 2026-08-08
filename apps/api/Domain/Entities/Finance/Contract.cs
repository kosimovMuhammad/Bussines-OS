using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

// Click-to-sign audit-trail MVP, not real e-signature/crypto (per existing product
// decision) - Signed*/SignedAt/SignedIpAddress below are the persisted "simple
// confirmation" record; ContractsService.SignAsync ALSO writes an AuditLog row through the
// same IAuditLogService every other sensitive action in this codebase uses, rather than
// this entity being the only record of who signed what.
public class Contract : IActivityLoggable, IArchivable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string CounterpartyName { get; set; } = string.Empty;
    public string? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public string? DealId { get; set; }
    public Deal? Deal { get; set; }
    public string Content { get; set; } = string.Empty;
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? TemplateId { get; set; }
    public Template? Template { get; set; }

    public DateTime? SignedAt { get; set; }
    public string? SignedByUserId { get; set; }
    public User? SignedByUser { get; set; }
    public string? SignedIpAddress { get; set; }
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
