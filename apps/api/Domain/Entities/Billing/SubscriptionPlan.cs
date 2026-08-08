namespace BusinessOS.Domain.Entities;

// Named SubscriptionPlan (not "Plan") to avoid colliding with the existing
// Domain.Enums.Plan enum still backing the legacy Company.Plan field. Global catalog,
// not tenant-scoped - every tenant sees the same set of plans, same reasoning as
// Permission.cs.
public class SubscriptionPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceMonthly { get; set; }
    public decimal PriceYearly { get; set; }
    public string Currency { get; set; } = "TJS";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SubscriptionPlanFeature> Features { get; set; } = new List<SubscriptionPlanFeature>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
