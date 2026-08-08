using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

// SubscriptionPlanFeature.Enabled is the sole source of truth - no company-level override
// table in this pass. Nothing in the spec named a concrete per-company override use case,
// and adding one speculatively would be schema the app can't yet exercise; a real request
// for it (e.g. a one-off enterprise deal) can add the table later without disturbing this
// service's shape.
public class FeatureAccessService(IAppDbContext db) : IFeatureAccessService
{
    public async Task<bool> HasFeatureAsync(string companyId, string featureKey, CancellationToken cancellationToken = default)
    {
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (subscription is null)
        {
            return false;
        }

        return await db.SubscriptionPlanFeatures
            .Where(f => f.PlanId == subscription.PlanId && f.FeatureKey == featureKey)
            .Select(f => f.Enabled)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetFeatureFlagsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (subscription is null)
        {
            return FeatureKeys.All.ToDictionary(k => k, _ => false);
        }

        var enabled = await db.SubscriptionPlanFeatures
            .Where(f => f.PlanId == subscription.PlanId)
            .ToDictionaryAsync(f => f.FeatureKey, f => f.Enabled, cancellationToken);

        return FeatureKeys.All.ToDictionary(k => k, k => enabled.GetValueOrDefault(k));
    }
}
