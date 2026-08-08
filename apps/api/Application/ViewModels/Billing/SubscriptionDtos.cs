using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Billing;

public record SubscriptionDto(
    string Id,
    string PlanId,
    string PlanName,
    string Status,
    string BillingCycle,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime? TrialEndsAt,
    DateTime? CanceledAt,
    string? PendingPlanId,
    bool CancelAtPeriodEnd);

public record ChangePlanRequest(string NewPlanId, BillingCycle BillingCycle);

public record ChangePlanResultDto(SubscriptionDto Subscription, bool Applied, decimal? ProratedAmount);

public record CancelSubscriptionRequest(bool Immediate = false);

public record RequestUpgradeRequest(string PlanId, BillingCycle BillingCycle);

// Status is always "pending" today - no payment provider is wired up yet (see
// IPaymentProvider/UnconfiguredPaymentProvider), so this never resolves to "applied" in
// this phase. The field exists so the response shape doesn't have to change once a real
// provider lands.
public record UpgradeRequestResultDto(
    string Status,
    string Message,
    string RequestedPlanId,
    string RequestedPlanName,
    string BillingCycle,
    decimal Amount,
    string Currency,
    string PaymentTransactionId);

public class ChangePlanRequestValidator : AbstractValidator<ChangePlanRequest>
{
    public ChangePlanRequestValidator()
    {
        RuleFor(x => x.NewPlanId).NotEmpty();
        RuleFor(x => x.BillingCycle).IsInEnum();
    }
}

public class RequestUpgradeRequestValidator : AbstractValidator<RequestUpgradeRequest>
{
    public RequestUpgradeRequestValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.BillingCycle).IsInEnum();
    }
}
