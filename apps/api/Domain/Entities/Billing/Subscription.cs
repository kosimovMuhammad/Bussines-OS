using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class Subscription : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string PlanId { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; } = null!;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Scheduled plan change (downgrades take effect at period end, not immediately -
    // upgrades apply PlanId directly instead of going through this field). Kept on the
    // same billing cycle as today; changing cycle mid-downgrade isn't supported.
    public string? PendingPlanId { get; set; }
    public SubscriptionPlan? PendingPlan { get; set; }

    // Cancellation requested but deferred to the end of the current period (the default
    // per spec). Immediate cancellation instead sets Status=Canceled directly and never
    // touches this flag.
    public bool CancelAtPeriodEnd { get; set; }

    public ICollection<SubscriptionInvoice> Invoices { get; set; } = new List<SubscriptionInvoice>();
}
