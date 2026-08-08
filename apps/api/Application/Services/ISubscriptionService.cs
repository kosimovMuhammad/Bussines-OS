using BusinessOS.Application.ViewModels.Billing;

namespace BusinessOS.Application.Services;

public interface ISubscriptionService
{
    Task<SubscriptionDto> GetCurrentAsync(string companyId, CancellationToken cancellationToken = default);

    Task<ChangePlanResultDto> ChangePlanAsync(string companyId, string actorUserId, ChangePlanRequest request, CancellationToken cancellationToken = default);

    Task<SubscriptionDto> CancelAsync(string companyId, string actorUserId, CancelSubscriptionRequest request, CancellationToken cancellationToken = default);

    // Records the intent to upgrade and leaves the current plan untouched - the actual
    // plan swap only happens once a real payment provider confirms the charge (a future
    // phase). Distinct from ChangePlanAsync, which applies upgrades immediately because it
    // predates the payment-gating requirement.
    Task<UpgradeRequestResultDto> RequestUpgradeAsync(string companyId, string actorUserId, RequestUpgradeRequest request, CancellationToken cancellationToken = default);
}
