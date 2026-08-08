using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// One row per (ProductId, BranchId) - BranchId is nullable for companies that don't use
// the Organization module's Branch entity (see Branch.cs), in which case a product has a
// single company-wide stock row. Quantity only ever changes through InventoryService's
// AdjustStockAsync (stock-in/stock-out/PATCH correction all funnel through it), which is
// what keeps InventoryMovement a complete ledger instead of a partial one.
public class Inventory : IActivityLoggable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string ProductId { get; set; } = string.Empty;
    public Product Product { get; set; } = null!;
    public string? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}
