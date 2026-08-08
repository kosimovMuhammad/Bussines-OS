using BusinessOS.Application.ViewModels.Billing;

namespace BusinessOS.Application.Services;

public interface ILimitEnforcementService
{
    // Throws LimitExceededException if the company is at/over its plan's limit for this
    // hard-capped metric. No-op if the plan has no limit defined for it (unlimited) or the
    // company has no subscription (fails open rather than blocking on a data gap).
    // Called from the service/command layer, not just the controller - defense in depth,
    // same reasoning as the RBAC resource-permission checks already in this codebase.
    Task CheckLimitAsync(string companyId, string metricKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsageMetricDto>> GetUsageAsync(string companyId, CancellationToken cancellationToken = default);

    // Returns the hard-capped metric keys where the company's CURRENT usage already
    // exceeds the target plan's limit - used to block downgrades that would strand a
    // company over its new plan's caps, without needing a new "restricted access" concept.
    Task<IReadOnlyList<string>> FindLimitViolationsAsync(string companyId, string targetPlanId, CancellationToken cancellationToken = default);
}
