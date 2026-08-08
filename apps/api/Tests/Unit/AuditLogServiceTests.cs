using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Audit;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class AuditLogServiceTests
{
    private static (TestDb Db, AuditLogService Service, Company Company, User User) CreateSut()
    {
        var db = new TestDb();
        var service = new AuditLogService(db.Context);

        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        return (db, service, company, user);
    }

    [Fact]
    public async Task Log_StagesRow_VisibleAfterCallerSaves()
    {
        var (db, service, company, user) = CreateSut();

        service.Log(company.Id, user.Id, "user.updated", "User", user.Id, new { name = "New Name" });
        await db.Context.SaveChangesAsync();

        var entry = Assert.Single(db.Context.AuditLogs);
        Assert.Equal("user.updated", entry.Action);
        Assert.Contains("New Name", entry.MetadataJson);
    }

    [Fact]
    public async Task ListAsync_AsEmployee_Throws()
    {
        var (_, service, company, _) = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ListAsync(company.Id, Role.Employee, new QueryAuditLogRequest()));
    }

    [Fact]
    public async Task ListAsync_AsOwner_ReturnsEntries()
    {
        var (db, service, company, user) = CreateSut();
        service.Log(company.Id, user.Id, "settings.updated", "Company", company.Id);
        await db.Context.SaveChangesAsync();

        var result = await service.ListAsync(company.Id, Role.Owner, new QueryAuditLogRequest());

        Assert.Single(result.Data);
    }

    [Fact]
    public async Task GetSecurityHistoryAsync_OnlyReturnsSecurityNamespacedActions()
    {
        var (db, service, company, user) = CreateSut();
        service.Log(company.Id, user.Id, "security.sessions_revoked", "User", user.Id);
        service.Log(company.Id, user.Id, "user.updated", "User", user.Id);
        await db.Context.SaveChangesAsync();

        var result = await service.GetSecurityHistoryAsync(company.Id, user.Id, new QueryAuditLogRequest());

        var entry = Assert.Single(result.Data);
        Assert.Equal("security.sessions_revoked", entry.Action);
    }
}
