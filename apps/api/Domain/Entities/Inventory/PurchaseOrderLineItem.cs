namespace BusinessOS.Domain.Entities;

// Owned child row, no CompanyId of its own - same shape as WorkflowAction/
// SubscriptionPlanFeature. Reachable (and tenant-scoped) only through PurchaseOrder.
public class PurchaseOrderLineItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PurchaseOrderId { get; set; } = string.Empty;
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public string ProductId { get; set; } = string.Empty;
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
