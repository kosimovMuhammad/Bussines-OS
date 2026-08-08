using BusinessOS.Application.ViewModels.Billing;

namespace BusinessOS.Application.Services;

public interface ISubscriptionPlanService
{
    Task<IReadOnlyList<SubscriptionPlanDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanDto> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanDto> CreateAsync(string actorUserId, CreateSubscriptionPlanRequest request, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanDto> UpdateAsync(string actorUserId, string id, UpdateSubscriptionPlanRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string actorUserId, string id, CancellationToken cancellationToken = default);
}
