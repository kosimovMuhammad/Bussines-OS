using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Company;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class TransferOwnershipTests
{
    private const string Password = "correct-password";

    private static async Task<(TestDb Db, CompanyService Service, Company Company, User Owner, User Target)> CreateSutAsync()
    {
        var db = new TestDb();
        var hasher = new FakePasswordHasher();
        var service = new CompanyService(db.Context, new AuditLogService(db.Context), hasher);

        var company = new Company { Name = "Acme" };
        var owner = new User { CompanyId = company.Id, Name = "Owner", Email = "owner@acme.tj", Role = Role.Owner, PasswordHash = await hasher.HashAsync(Password) };
        var target = new User { CompanyId = company.Id, Name = "Target", Email = "target@acme.tj", Role = Role.Employee, Status = UserStatus.Active };

        db.Context.Companies.Add(company);
        db.Context.Users.AddRange(owner, target);
        await db.Context.SaveChangesAsync();

        return (db, service, company, owner, target);
    }

    [Fact]
    public async Task TransferOwnership_ValidRequest_SwapsRolesAndLogsAudit()
    {
        var (db, service, company, owner, target) = await CreateSutAsync();

        await service.TransferOwnershipAsync(company.Id, owner.Id, new TransferOwnershipRequest(target.Id, Password));

        var refreshedOwner = await db.Context.Users.FindAsync(owner.Id);
        var refreshedTarget = await db.Context.Users.FindAsync(target.Id);
        Assert.Equal(Role.Admin, refreshedOwner!.Role);
        Assert.Equal(Role.Owner, refreshedTarget!.Role);

        var auditEntry = Assert.Single(db.Context.AuditLogs);
        Assert.Equal("security.ownership_transferred", auditEntry.Action);
    }

    [Fact]
    public async Task TransferOwnership_WrongPassword_ThrowsAndPersistsNothing()
    {
        var (db, service, company, owner, target) = await CreateSutAsync();

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.TransferOwnershipAsync(company.Id, owner.Id, new TransferOwnershipRequest(target.Id, "wrong-password")));

        var refreshedOwner = await db.Context.Users.FindAsync(owner.Id);
        var refreshedTarget = await db.Context.Users.FindAsync(target.Id);
        Assert.Equal(Role.Owner, refreshedOwner!.Role);
        Assert.Equal(Role.Employee, refreshedTarget!.Role);
        Assert.Empty(db.Context.AuditLogs);
    }

    [Fact]
    public async Task TransferOwnership_ActorNotOwner_ThrowsAndPersistsNothing()
    {
        var db = new TestDb();
        var hasher = new FakePasswordHasher();
        var service = new CompanyService(db.Context, new AuditLogService(db.Context), hasher);

        var company = new Company { Name = "Acme" };
        var admin = new User { CompanyId = company.Id, Name = "Admin", Email = "admin@acme.tj", Role = Role.Admin, PasswordHash = await hasher.HashAsync(Password) };
        var target = new User { CompanyId = company.Id, Name = "Target", Email = "target@acme.tj", Role = Role.Employee, Status = UserStatus.Active };
        db.Context.Companies.Add(company);
        db.Context.Users.AddRange(admin, target);
        await db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.TransferOwnershipAsync(company.Id, admin.Id, new TransferOwnershipRequest(target.Id, Password)));

        Assert.Empty(db.Context.AuditLogs);
    }

    [Fact]
    public async Task TransferOwnership_InactiveTarget_ThrowsAndPersistsNothing()
    {
        var (db, service, company, owner, target) = await CreateSutAsync();
        target.Status = UserStatus.Suspended;
        await db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.TransferOwnershipAsync(company.Id, owner.Id, new TransferOwnershipRequest(target.Id, Password)));

        var refreshedOwner = await db.Context.Users.FindAsync(owner.Id);
        Assert.Equal(Role.Owner, refreshedOwner!.Role);
    }

    [Fact]
    public async Task TransferOwnership_AlreadyOwner_Throws()
    {
        var (_, service, company, owner, _) = await CreateSutAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.TransferOwnershipAsync(company.Id, owner.Id, new TransferOwnershipRequest(owner.Id, Password)));
    }
}
