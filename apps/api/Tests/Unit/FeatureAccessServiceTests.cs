using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class FeatureAccessServiceTests
{
    private static (TestDb Db, FeatureAccessService Service, Company Company) CreateSut()
    {
        var db = new TestDb();
        var plan = new SubscriptionPlan { Id = "plan-pro", Name = "Pro" };
        plan.Features.Add(new SubscriptionPlanFeature { PlanId = plan.Id, FeatureKey = FeatureKeys.AiFeatures, Enabled = true });
        plan.Features.Add(new SubscriptionPlanFeature { PlanId = plan.Id, FeatureKey = FeatureKeys.CustomBranding, Enabled = false });
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

        return (db, new FeatureAccessService(db.Context), company);
    }

    [Fact]
    public async Task HasFeatureAsync_EnabledOnPlan_ReturnsTrue()
    {
        var (_, service, company) = CreateSut();

        Assert.True(await service.HasFeatureAsync(company.Id, FeatureKeys.AiFeatures));
    }

    [Fact]
    public async Task HasFeatureAsync_DisabledOnPlan_ReturnsFalse()
    {
        var (_, service, company) = CreateSut();

        Assert.False(await service.HasFeatureAsync(company.Id, FeatureKeys.CustomBranding));
    }

    [Fact]
    public async Task HasFeatureAsync_KeyNotDefinedOnPlan_ReturnsFalse()
    {
        var (_, service, company) = CreateSut();

        Assert.False(await service.HasFeatureAsync(company.Id, FeatureKeys.ApiAccess));
    }

    [Fact]
    public async Task HasFeatureAsync_NoSubscription_ReturnsFalse()
    {
        using var db = new TestDb();
        var service = new FeatureAccessService(db.Context);

        Assert.False(await service.HasFeatureAsync("no-such-company", FeatureKeys.AiFeatures));
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_ReturnsAllKnownKeysWithDefaultFalseForUnset()
    {
        var (_, service, company) = CreateSut();

        var flags = await service.GetFeatureFlagsAsync(company.Id);

        Assert.Equal(FeatureKeys.All.Count, flags.Count);
        Assert.True(flags[FeatureKeys.AiFeatures]);
        Assert.False(flags[FeatureKeys.CustomBranding]);
        Assert.False(flags[FeatureKeys.ApiAccess]);
    }
}
