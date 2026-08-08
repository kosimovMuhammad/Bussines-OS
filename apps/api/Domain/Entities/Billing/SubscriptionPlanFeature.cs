namespace BusinessOS.Domain.Entities;

public class SubscriptionPlanFeature
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlanId { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; } = null!;
    public string FeatureKey { get; set; } = string.Empty;
    public int? Limit { get; set; }
    public bool Enabled { get; set; }
}
