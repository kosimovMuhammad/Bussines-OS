using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class UsersServiceLoginHistoryTests
{
    private static (TestDb Db, UsersService Service, Company Company, User Target) CreateSut()
    {
        var db = new TestDb();
        var service = new UsersService(db.Context, new FakePasswordHasher(), new NoopBackgroundTaskQueue(), new AuditLogService(db.Context), new NotificationService(db.Context), new NoopLimitEnforcementService());

        var company = new Company { Name = "Acme" };
        var target = new User { CompanyId = company.Id, Name = "Bob", Email = "bob@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(target);
        db.Context.SaveChanges();

        db.Context.LoginHistories.Add(new LoginHistory { UserId = target.Id, CompanyId = company.Id, Success = true });
        db.Context.SaveChanges();

        return (db, service, company, target);
    }

    [Fact]
    public async Task GetLoginHistory_AsOwner_Succeeds()
    {
        var (_, service, company, target) = CreateSut();

        var result = await service.GetLoginHistoryAsync(company.Id, Role.Owner, target.Id, new QueryLoginHistoryRequest());

        Assert.Single(result.Data);
    }

    [Fact]
    public async Task GetLoginHistory_AsEmployee_Throws()
    {
        var (_, service, company, target) = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetLoginHistoryAsync(company.Id, Role.Employee, target.Id, new QueryLoginHistoryRequest()));
    }
}
