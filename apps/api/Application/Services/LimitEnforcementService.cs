using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels.Billing;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class LimitEnforcementService(IAppDbContext db) : ILimitEnforcementService
{
    public async Task CheckLimitAsync(string companyId, string metricKey, CancellationToken cancellationToken = default)
    {
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        if (subscription is null)
        {
            return;
        }

        var limit = await db.SubscriptionPlanFeatures
            .Where(f => f.PlanId == subscription.PlanId && f.FeatureKey == metricKey)
            .Select(f => f.Limit)
            .SingleOrDefaultAsync(cancellationToken);

        if (limit is null)
        {
            return;
        }

        var current = await CountCurrentUsageAsync(companyId, metricKey, cancellationToken);
        if (current >= limit.Value)
        {
            throw new LimitExceededException(metricKey, limit.Value, current);
        }
    }

    public async Task<IReadOnlyList<UsageMetricDto>> GetUsageAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("No subscription found for this company");

        var features = await db.SubscriptionPlanFeatures
            .Where(f => f.PlanId == subscription.PlanId)
            .ToDictionaryAsync(f => f.FeatureKey, f => f.Limit, cancellationToken);

        var result = new List<UsageMetricDto>();
        foreach (var key in UsageMetricKeys.HardCapped)
        {
            var current = await CountCurrentUsageAsync(companyId, key, cancellationToken);
            var limit = features.GetValueOrDefault(key);
            result.Add(new UsageMetricDto(key, current, limit, limit is null));
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> FindLimitViolationsAsync(string companyId, string targetPlanId, CancellationToken cancellationToken = default)
    {
        var features = await db.SubscriptionPlanFeatures
            .Where(f => f.PlanId == targetPlanId)
            .ToDictionaryAsync(f => f.FeatureKey, f => f.Limit, cancellationToken);

        var violations = new List<string>();
        foreach (var key in UsageMetricKeys.HardCapped)
        {
            var limit = features.GetValueOrDefault(key);
            if (limit is null)
            {
                continue;
            }

            var current = await CountCurrentUsageAsync(companyId, key, cancellationToken);
            if (current > limit.Value)
            {
                violations.Add(key);
            }
        }

        return violations;
    }

    // Live counts computed directly from the source tables, not a maintained counter -
    // avoids drift entirely (no increment/decrement calls scattered across every
    // create/delete path), matching UsageCounter's own "Live" kind description. The
    // UsageCounter table itself is reserved for Periodic metrics (not wired up in this
    // pass - see UsageMetricKeys' soft-metric note).
    private async Task<int> CountCurrentUsageAsync(string companyId, string metricKey, CancellationToken cancellationToken) => metricKey switch
    {
        UsageMetricKeys.MaxUsers => await db.Users.CountAsync(u => u.CompanyId == companyId, cancellationToken),
        UsageMetricKeys.MaxContacts => await db.Contacts.CountAsync(c => c.CompanyId == companyId, cancellationToken),
        UsageMetricKeys.MaxDeals => await db.Deals.CountAsync(d => d.CompanyId == companyId, cancellationToken),
        UsageMetricKeys.AutomationWorkflows => await db.Workflows.CountAsync(w => w.CompanyId == companyId, cancellationToken),
        _ => 0,
    };
}
