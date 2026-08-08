using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class SubscriptionPlanServiceTests
{
    private static (TestDb Db, SubscriptionPlanService Service, User Admin, User Regular) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var admin = new User { CompanyId = company.Id, Name = "Admin", Email = "admin@acme.tj", IsPlatformAdmin = true };
        var regular = new User { CompanyId = company.Id, Name = "Regular", Email = "regular@acme.tj", IsPlatformAdmin = false };
        db.Context.Companies.Add(company);
        db.Context.Users.AddRange(admin, regular);
        db.Context.SaveChanges();

        return (db, new SubscriptionPlanService(db.Context), admin, regular);
    }

    private static CreateSubscriptionPlanRequest StarterRequest() => new(
        "Starter", "For small teams", 29m, 290m, "TJS", true, 1,
        [new CreateSubscriptionPlanFeatureRequest("max_users", 5, true)]);

    [Fact]
    public async Task CreateAsync_AsPlatformAdmin_Succeeds()
    {
        var (db, service, admin, _) = CreateSut();

        var created = await service.CreateAsync(admin.Id, StarterRequest());

        Assert.Equal("Starter", created.Name);
        Assert.Single(created.Features);
        Assert.Single(db.Context.SubscriptionPlans);
    }

    [Fact]
    public async Task CreateAsync_AsRegularUser_Throws()
    {
        var (_, service, _, regular) = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(regular.Id, StarterRequest()));
    }

    [Fact]
    public async Task ListAsync_ExcludesInactiveByDefault()
    {
        var (db, service, admin, _) = CreateSut();
        await service.CreateAsync(admin.Id, StarterRequest());
        await service.CreateAsync(admin.Id, StarterRequest() with { Name = "Discontinued", IsActive = false });

        var activeOnly = await service.ListAsync(includeInactive: false);
        var all = await service.ListAsync(includeInactive: true);

        Assert.Single(activeOnly);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesFeaturesWholesale()
    {
        var (db, service, admin, _) = CreateSut();
        var created = await service.CreateAsync(admin.Id, StarterRequest());

        var updated = await service.UpdateAsync(admin.Id, created.Id, new UpdateSubscriptionPlanRequest(
            null, null, null, null, null, null, null,
            [new CreateSubscriptionPlanFeatureRequest("max_users", 10, true), new CreateSubscriptionPlanFeatureRequest("api_access", null, true)]));

        Assert.Equal(2, updated.Features.Count);
        Assert.Equal(2, db.Context.SubscriptionPlanFeatures.Count());
    }

    [Fact]
    public async Task RemoveAsync_PlanInUse_Throws()
    {
        var (db, service, admin, _) = CreateSut();
        var created = await service.CreateAsync(admin.Id, StarterRequest());

        var company = db.Context.Companies.Single();
        db.Context.Subscriptions.Add(new Subscription
        {
            CompanyId = company.Id,
            PlanId = created.Id,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
        });
        db.Context.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => service.RemoveAsync(admin.Id, created.Id));
    }

    [Fact]
    public async Task RemoveAsync_PlanNotInUse_Succeeds()
    {
        var (db, service, admin, _) = CreateSut();
        var created = await service.CreateAsync(admin.Id, StarterRequest());

        await service.RemoveAsync(admin.Id, created.Id);

        Assert.Empty(db.Context.SubscriptionPlans);
    }
}
