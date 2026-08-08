using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Payments;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class SubscriptionChangePlanTests
{
    private static (TestDb Db, SubscriptionService Service, Company Company, User User) CreateSut(
        ILimitEnforcementService? limitEnforcement = null, DateTime? currentPeriodEnd = null)
    {
        var db = new TestDb();
        var starter = new SubscriptionPlan { Id = "plan-starter", Name = "Starter", PriceMonthly = 250, PriceYearly = 2500 };
        var pro = new SubscriptionPlan { Id = "plan-pro", Name = "Pro", PriceMonthly = 650, PriceYearly = 6500 };
        db.Context.SubscriptionPlans.AddRange(starter, pro);

        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        db.Context.Subscriptions.Add(new Subscription
        {
            CompanyId = company.Id,
            PlanId = "plan-starter",
            Status = SubscriptionStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = currentPeriodEnd ?? DateTime.UtcNow.AddDays(15),
        });
        db.Context.SaveChanges();

        var service = new SubscriptionService(db.Context, limitEnforcement ?? new NoopLimitEnforcementService(), new AuditLogService(db.Context), new UnconfiguredPaymentProvider());
        return (db, service, company, user);
    }

    [Fact]
    public async Task ChangePlanAsync_Upgrade_AppliesImmediatelyAndProrates()
    {
        var (db, service, company, user) = CreateSut();

        var result = await service.ChangePlanAsync(company.Id, user.Id, new ChangePlanRequest("plan-pro", BillingCycle.Monthly));

        Assert.True(result.Applied);
        Assert.NotNull(result.ProratedAmount);
        Assert.Equal("plan-pro", result.Subscription.PlanId);
        var subscription = Assert.Single(db.Context.Subscriptions);
        Assert.Equal("plan-pro", subscription.PlanId);
        Assert.Null(subscription.PendingPlanId);
    }

    [Fact]
    public async Task ChangePlanAsync_Downgrade_SchedulesForPeriodEndInsteadOfApplyingNow()
    {
        var (db, service, company, user) = CreateSut();
        await service.ChangePlanAsync(company.Id, user.Id, new ChangePlanRequest("plan-pro", BillingCycle.Monthly));

        var result = await service.ChangePlanAsync(company.Id, user.Id, new ChangePlanRequest("plan-starter", BillingCycle.Monthly));

        Assert.False(result.Applied);
        Assert.Equal("plan-pro", result.Subscription.PlanId);
        Assert.Equal("plan-starter", result.Subscription.PendingPlanId);
        var subscription = Assert.Single(db.Context.Subscriptions);
        Assert.Equal("plan-pro", subscription.PlanId);
        Assert.Equal("plan-starter", subscription.PendingPlanId);
    }

    [Fact]
    public async Task ChangePlanAsync_DowngradeOverLimit_Throws()
    {
        var (db, service, company, user) = CreateSut(new AlwaysViolatingLimitEnforcementService());
        await service.ChangePlanAsync(company.Id, user.Id, new ChangePlanRequest("plan-pro", BillingCycle.Monthly));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ChangePlanAsync(company.Id, user.Id, new ChangePlanRequest("plan-starter", BillingCycle.Monthly)));

        var subscription = Assert.Single(db.Context.Subscriptions);
        Assert.Equal("plan-pro", subscription.PlanId);
        Assert.Null(subscription.PendingPlanId);
    }

    [Fact]
    public async Task ChangePlanAsync_PastPeriodEnd_AppliesPendingDowngradeAndRollsPeriodBeforeChangingAgain()
    {
        var (db, service, company, user) = CreateSut(currentPeriodEnd: DateTime.UtcNow.AddDays(-1));
        var subscription = db.Context.Subscriptions.Single();
        subscription.PendingPlanId = "plan-pro";
        db.Context.SaveChanges();
        var originalPeriodEnd = subscription.CurrentPeriodEnd;

        var dto = await service.GetCurrentAsync(company.Id);

        Assert.Equal("plan-pro", dto.PlanId);
        Assert.Null(dto.PendingPlanId);
        Assert.True(dto.CurrentPeriodEnd > originalPeriodEnd);
    }

    [Fact]
    public async Task CancelAsync_Immediate_CancelsRightAway()
    {
        var (db, service, company, user) = CreateSut();

        var dto = await service.CancelAsync(company.Id, user.Id, new CancelSubscriptionRequest(Immediate: true));

        Assert.Equal("Canceled", dto.Status);
        Assert.NotNull(dto.CanceledAt);
        var subscription = Assert.Single(db.Context.Subscriptions);
        Assert.Equal(SubscriptionStatus.Canceled, subscription.Status);
    }

    [Fact]
    public async Task CancelAsync_EndOfPeriod_StaysActiveUntilPeriodElapsesThenCancels()
    {
        var (db, service, company, user) = CreateSut();

        var dto = await service.CancelAsync(company.Id, user.Id, new CancelSubscriptionRequest(Immediate: false));
        Assert.Equal("Active", dto.Status);
        Assert.True(dto.CancelAtPeriodEnd);

        // Simulate time passing: the period that was current when cancellation was
        // requested has now elapsed - the next read should settle it as Canceled.
        var subscription = db.Context.Subscriptions.Single();
        subscription.CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1);
        db.Context.SaveChanges();

        var settled = await service.GetCurrentAsync(company.Id);
        Assert.Equal("Canceled", settled.Status);
    }
}

public class AlwaysViolatingLimitEnforcementService : ILimitEnforcementService
{
    public Task CheckLimitAsync(string companyId, string metricKey, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<UsageMetricDto>> GetUsageAsync(string companyId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UsageMetricDto>>([]);

    public Task<IReadOnlyList<string>> FindLimitViolationsAsync(string companyId, string targetPlanId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(["max_users"]);
}
