using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

// Append-only ledger, direct CompanyId (not just reachable via Inventory) so it can be
// queried company-wide on its own, same shape as AuditLog/ActivityLog/LoginHistory. Not
// IActivityLoggable itself - it IS the audit trail for Inventory.Quantity changes, so
// logging its own creation to ActivityLog too would be redundant with the Inventory
// "Updated" entry the same save already produces.
public class InventoryMovement : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string InventoryId { get; set; } = string.Empty;
    public Inventory Inventory { get; set; } = null!;
    public InventoryMovementDirection Direction { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
