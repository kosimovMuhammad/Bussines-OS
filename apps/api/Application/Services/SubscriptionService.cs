using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class SubscriptionService(
    IAppDbContext db,
    ILimitEnforcementService limitEnforcement,
    IAuditLogService auditLog,
    IPaymentProvider paymentProvider) : ISubscriptionService
{
    public async Task<SubscriptionDto> GetCurrentAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAsync(companyId, cancellationToken);
        await ApplyRolloverIfDueAsync(subscription, cancellationToken);

        return ToDto(subscription);
    }

    public async Task<ChangePlanResultDto> ChangePlanAsync(string companyId, string actorUserId, ChangePlanRequest request, CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAsync(companyId, cancellationToken);
        await ApplyRolloverIfDueAsync(subscription, cancellationToken);

        var newPlan = await db.SubscriptionPlans.SingleOrDefaultAsync(p => p.Id == request.NewPlanId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found");

        if (newPlan.Id == subscription.PlanId && request.BillingCycle == subscription.BillingCycle && subscription.PendingPlanId is null)
        {
            return new ChangePlanResultDto(ToDto(subscription), false, null);
        }

        var oldPrice = subscription.BillingCycle == BillingCycle.Monthly ? subscription.Plan.PriceMonthly : subscription.Plan.PriceYearly;
        // Priced at the CURRENT billing cycle for the upgrade/downgrade decision and for
        // the pending-downgrade path - a cycle change only actually takes effect together
        // with an immediate upgrade below. A downgrade request that also asks for a cycle
        // change ignores the cycle part; only the plan change is scheduled.
        var newPriceAtCurrentCycle = subscription.BillingCycle == BillingCycle.Monthly ? newPlan.PriceMonthly : newPlan.PriceYearly;
        var newPrice = request.BillingCycle == BillingCycle.Monthly ? newPlan.PriceMonthly : newPlan.PriceYearly;
        var isUpgrade = newPriceAtCurrentCycle >= oldPrice;

        if (isUpgrade)
        {
            var proratedAmount = ProrationCalculator.CalculateProratedAmount(
                oldPrice, newPrice, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, DateTime.UtcNow);

            subscription.PlanId = newPlan.Id;
            subscription.Plan = newPlan;
            subscription.BillingCycle = request.BillingCycle;
            subscription.PendingPlanId = null;

            auditLog.Log(companyId, actorUserId, "subscription.upgraded", "Subscription", subscription.Id,
                new { toPlanId = newPlan.Id, request.BillingCycle, proratedAmount });
            await db.SaveChangesAsync(cancellationToken);

            return new ChangePlanResultDto(ToDto(subscription), true, proratedAmount);
        }
        else
        {
            var violations = await limitEnforcement.FindLimitViolationsAsync(companyId, newPlan.Id, cancellationToken);
            if (violations.Count > 0)
            {
                throw new ConflictException($"Cannot downgrade: current usage exceeds the target plan's limits for: {string.Join(", ", violations)}");
            }

            subscription.PendingPlanId = newPlan.Id;

            auditLog.Log(companyId, actorUserId, "subscription.downgrade_scheduled", "Subscription", subscription.Id,
                new { toPlanId = newPlan.Id, effectiveAt = subscription.CurrentPeriodEnd });
            await db.SaveChangesAsync(cancellationToken);

            return new ChangePlanResultDto(ToDto(subscription), false, null);
        }
    }

    public async Task<SubscriptionDto> CancelAsync(string companyId, string actorUserId, CancelSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAsync(companyId, cancellationToken);
        await ApplyRolloverIfDueAsync(subscription, cancellationToken);

        if (request.Immediate)
        {
            subscription.Status = SubscriptionStatus.Canceled;
            subscription.CanceledAt = DateTime.UtcNow;
            subscription.CancelAtPeriodEnd = false;
            subscription.PendingPlanId = null;

            auditLog.Log(companyId, actorUserId, "subscription.canceled_immediately", "Subscription", subscription.Id, null);
        }
        else
        {
            subscription.CancelAtPeriodEnd = true;
            subscription.PendingPlanId = null;

            auditLog.Log(companyId, actorUserId, "subscription.cancel_scheduled", "Subscription", subscription.Id,
                new { effectiveAt = subscription.CurrentPeriodEnd });
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(subscription);
    }

    public async Task<UpgradeRequestResultDto> RequestUpgradeAsync(string companyId, string actorUserId, RequestUpgradeRequest request, CancellationToken cancellationToken = default)
    {
        var subscription = await LoadAsync(companyId, cancellationToken);

        var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException("Plan not found");

        var amount = request.BillingCycle == BillingCycle.Monthly ? plan.PriceMonthly : plan.PriceYearly;

        // Stub: no real Payme/Click/Alif integration in this phase (Stripe isn't viable for
        // this market). We record the transaction as Pending and stop there - applying the
        // plan change happens once a real provider confirms payment, not here.
        var transaction = new PaymentTransaction
        {
            CompanyId = companyId,
            Provider = paymentProvider.ProviderName,
            Amount = amount,
            Currency = plan.Currency,
            Status = PaymentTransactionStatus.Pending,
        };
        db.PaymentTransactions.Add(transaction);

        auditLog.Log(companyId, actorUserId, "subscription.upgrade_requested", "Subscription", subscription.Id,
            new { requestedPlanId = plan.Id, request.BillingCycle, amount, currency = plan.Currency, paymentTransactionId = transaction.Id });

        await db.SaveChangesAsync(cancellationToken);

        return new UpgradeRequestResultDto(
            "pending",
            "Payment processing is pending - Payme/Click/Alif integration is not implemented yet in this phase. The plan change will be applied once a payment provider confirms the charge.",
            plan.Id, plan.Name, request.BillingCycle.ToString(), amount, plan.Currency, transaction.Id);
    }

    private async Task<Subscription> LoadAsync(string companyId, CancellationToken cancellationToken) =>
        await db.Subscriptions.Include(s => s.Plan).SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("No subscription found for this company");

    // Lazily settles a subscription once its current period has elapsed: applies any
    // pending downgrade, finalizes a scheduled cancellation, or simply rolls the period
    // forward. Runs on every read/mutate rather than via a dedicated background job - no
    // scheduler infra exists for this yet (Billing Phase 7 will add one for invoicing and
    // can absorb this too), and correctness only matters the next time someone looks.
    private async Task ApplyRolloverIfDueAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow < subscription.CurrentPeriodEnd || subscription.Status == SubscriptionStatus.Canceled)
        {
            return;
        }

        if (subscription.CancelAtPeriodEnd)
        {
            subscription.Status = SubscriptionStatus.Canceled;
            subscription.CanceledAt = subscription.CurrentPeriodEnd;
            subscription.CancelAtPeriodEnd = false;
            subscription.PendingPlanId = null;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var wasTrialing = subscription.Status == SubscriptionStatus.Trialing;

        if (subscription.PendingPlanId is not null)
        {
            var pendingPlan = await db.SubscriptionPlans.SingleAsync(p => p.Id == subscription.PendingPlanId, cancellationToken);
            subscription.PlanId = pendingPlan.Id;
            subscription.Plan = pendingPlan;
            subscription.PendingPlanId = null;
        }

        var newPeriodLength = wasTrialing
            ? (subscription.BillingCycle == BillingCycle.Monthly ? TimeSpan.FromDays(30) : TimeSpan.FromDays(365))
            : subscription.CurrentPeriodEnd - subscription.CurrentPeriodStart;

        subscription.CurrentPeriodStart = subscription.CurrentPeriodEnd;
        subscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd.Add(newPeriodLength);
        if (wasTrialing)
        {
            subscription.Status = SubscriptionStatus.Active;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static SubscriptionDto ToDto(Subscription s) => new(
        s.Id, s.PlanId, s.Plan.Name, s.Status.ToString(), s.BillingCycle.ToString(), s.CurrentPeriodStart, s.CurrentPeriodEnd,
        s.TrialEndsAt, s.CanceledAt, s.PendingPlanId, s.CancelAtPeriodEnd);
}
