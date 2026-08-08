using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Auth;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Payments;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class SubscriptionLifecycleTests
{
    private static TestDb SeedProPlan()
    {
        var db = new TestDb();
        db.Context.SubscriptionPlans.Add(new SubscriptionPlan { Id = "plan-pro", Name = "Pro", PriceMonthly = 650, PriceYearly = 6500 });
        db.Context.SaveChanges();
        return db;
    }

    [Fact]
    public async Task RegisterAsync_CreatesTrialingSubscriptionOnProPlan()
    {
        using var db = SeedProPlan();
        var hasher = new FakePasswordHasher();
        var authService = new AuthService(db.Context, hasher, new FakeTokenService(), new RoleSeedingService(db.Context), new AuditLogService(db.Context), new NoopBackgroundTaskQueue());

        await authService.RegisterAsync(new RegisterRequest("Acme", "Alice", "alice@acme.tj", "password123", "Founder"));
        var companyId = db.Context.Users.Single(u => u.Email == "alice@acme.tj").CompanyId;

        var subscription = Assert.Single(db.Context.Subscriptions);
        Assert.Equal(companyId, subscription.CompanyId);
        Assert.Equal("plan-pro", subscription.PlanId);
        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.NotNull(subscription.TrialEndsAt);
        Assert.True(subscription.TrialEndsAt > DateTime.UtcNow.AddDays(13));
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsSubscriptionWithPlanName()
    {
        using var db = SeedProPlan();
        var hasher = new FakePasswordHasher();
        var authService = new AuthService(db.Context, hasher, new FakeTokenService(), new RoleSeedingService(db.Context), new AuditLogService(db.Context), new NoopBackgroundTaskQueue());
        var subscriptionService = new SubscriptionService(db.Context, new NoopLimitEnforcementService(), new AuditLogService(db.Context), new UnconfiguredPaymentProvider());

        await authService.RegisterAsync(new RegisterRequest("Acme", "Alice", "alice@acme.tj", "password123", "Founder"));
        var companyId = db.Context.Users.Single(u => u.Email == "alice@acme.tj").CompanyId;

        var dto = await subscriptionService.GetCurrentAsync(companyId);

        Assert.Equal("Pro", dto.PlanName);
        Assert.Equal("Trialing", dto.Status);
    }

    [Fact]
    public async Task GetCurrentAsync_NoSubscription_Throws()
    {
        using var db = new TestDb();
        var subscriptionService = new SubscriptionService(db.Context, new NoopLimitEnforcementService(), new AuditLogService(db.Context), new UnconfiguredPaymentProvider());

        await Assert.ThrowsAsync<NotFoundException>(() => subscriptionService.GetCurrentAsync("no-such-company"));
    }
}
