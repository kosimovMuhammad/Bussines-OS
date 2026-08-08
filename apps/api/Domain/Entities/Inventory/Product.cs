using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class Product : IActivityLoggable, IArchivable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public string Currency { get; set; } = "TJS";
    public string? CategoryId { get; set; }
    public Category? Category { get; set; }
    public ProductUnit Unit { get; set; } = ProductUnit.Piece;
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Inventory> InventoryRecords { get; set; } = new List<Inventory>();
    public ICollection<PurchaseOrderLineItem> PurchaseOrderLineItems { get; set; } = new List<PurchaseOrderLineItem>();
}
