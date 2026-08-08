using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class LimitEnforcementServiceTests
{
    private static (TestDb Db, LimitEnforcementService Service, Company Company) CreateSut(int? maxUsersLimit)
    {
        var db = new TestDb();
        var plan = new SubscriptionPlan { Id = "plan-x", Name = "X" };
        plan.Features.Add(new SubscriptionPlanFeature { PlanId = plan.Id, FeatureKey = UsageMetricKeys.MaxUsers, Limit = maxUsersLimit, Enabled = true });
        db.Context.SubscriptionPlans.Add(plan);

        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();

        db.Context.Subscriptions.Add(new Subscription
        {
            CompanyId = company.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
        });
        db.Context.SaveChanges();

        return (db, new LimitEnforcementService(db.Context), company);
    }

    private static void AddUsers(TestDb db, Company company, int count)
    {
        for (var i = 0; i < count; i++)
        {
            db.Context.Users.Add(new User { CompanyId = company.Id, Name = $"User {i}", Email = $"u{i}@acme.tj" });
        }

        db.Context.SaveChanges();
    }

    [Fact]
    public async Task CheckLimitAsync_UnderLimit_DoesNotThrow()
    {
        var (db, service, company) = CreateSut(maxUsersLimit: 5);
        AddUsers(db, company, 3);

        await service.CheckLimitAsync(company.Id, UsageMetricKeys.MaxUsers);
    }

    [Fact]
    public async Task CheckLimitAsync_AtLimit_ThrowsWithMetricDetails()
    {
        var (db, service, company) = CreateSut(maxUsersLimit: 5);
        AddUsers(db, company, 5);

        var ex = await Assert.ThrowsAsync<LimitExceededException>(() => service.CheckLimitAsync(company.Id, UsageMetricKeys.MaxUsers));

        Assert.Equal(UsageMetricKeys.MaxUsers, ex.MetricKey);
        Assert.Equal(5, ex.Limit);
        Assert.Equal(5, ex.Current);
    }

    [Fact]
    public async Task CheckLimitAsync_NullLimit_TreatedAsUnlimited_NeverThrows()
    {
        var (db, service, company) = CreateSut(maxUsersLimit: null);
        AddUsers(db, company, 500);

        await service.CheckLimitAsync(company.Id, UsageMetricKeys.MaxUsers);
    }

    [Fact]
    public async Task CheckLimitAsync_NoSubscription_FailsOpen()
    {
        using var db = new TestDb();
        var service = new LimitEnforcementService(db.Context);

        // No subscription at all for this company - must not block (fails open on the
        // data gap rather than locking everyone out).
        await service.CheckLimitAsync("no-such-company", UsageMetricKeys.MaxUsers);
    }

    [Fact]
    public async Task CheckLimitAsync_MetricNotDefinedOnPlan_TreatedAsUnlimited()
    {
        var (db, service, company) = CreateSut(maxUsersLimit: 5);

        // A metric the plan simply has no row for at all.
        await service.CheckLimitAsync(company.Id, UsageMetricKeys.MaxDeals);
    }

    [Fact]
    public async Task GetUsageAsync_ReportsCurrentValueAgainstLimit()
    {
        var (db, service, company) = CreateSut(maxUsersLimit: 5);
        AddUsers(db, company, 2);

        var usage = await service.GetUsageAsync(company.Id);

        var maxUsers = usage.Single(u => u.MetricKey == UsageMetricKeys.MaxUsers);
        Assert.Equal(2, maxUsers.CurrentValue);
        Assert.Equal(5, maxUsers.Limit);
        Assert.False(maxUsers.IsUnlimited);
    }
}
