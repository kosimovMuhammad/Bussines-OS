using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class EmployeePerformanceServiceTests
{
    private static (TestDb Db, EmployeePerformanceService Service, Company Company, User Owner, User Employee) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var owner = new User { CompanyId = company.Id, Name = "Owner", Email = "owner@acme.tj", Role = Role.Owner };
        var employee = new User { CompanyId = company.Id, Name = "Emp", Email = "emp@acme.tj", Role = Role.Employee };
        var contact = new Contact { CompanyId = company.Id, FirstName = "Client" };
        db.Context.Companies.Add(company);
        db.Context.Users.AddRange(owner, employee);
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();

        db.Context.Deals.AddRange(
            new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Won1", Stage = DealStage.Won, Value = 1000, OwnerId = employee.Id, CreatedAt = DateTime.UtcNow.AddDays(-10), UpdatedAt = DateTime.UtcNow.AddDays(-5) },
            new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Lost1", Stage = DealStage.Lost, Value = 300, OwnerId = employee.Id, CreatedAt = DateTime.UtcNow.AddDays(-8), UpdatedAt = DateTime.UtcNow.AddDays(-4) });

        db.Context.Tasks.AddRange(
            new TaskItem { CompanyId = company.Id, Title = "T1", Status = TaskItemStatus.Completed, AssignedTo = employee.Id, UpdatedAt = DateTime.UtcNow.AddDays(-2) },
            new TaskItem { CompanyId = company.Id, Title = "T2", Status = TaskItemStatus.Todo, AssignedTo = employee.Id, DueDate = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow.AddDays(-1) });
        db.Context.SaveChanges();

        return (db, new EmployeePerformanceService(db.Context), company, owner, employee);
    }

    [Fact]
    public async Task GetAsync_SelfView_AlwaysAllowed()
    {
        var (_, service, company, _, employee) = CreateSut();

        var result = await service.GetAsync(company.Id, employee.Id, Role.Employee, employee.Id, null, null);

        var dto = Assert.Single(result);
        Assert.Equal(1, dto.DealsWonCount);
        Assert.Equal(1000, dto.DealsWonValue);
        Assert.Equal(1, dto.DealsLostCount);
        Assert.Equal(300, dto.DealsLostValue);
        Assert.Equal(1, dto.TasksCompleted);
        Assert.Equal(1, dto.TasksOverdue);
        Assert.NotNull(dto.AvgDealCycleDays);
    }

    [Fact]
    public async Task GetAsync_CompanyWideView_RequiresOwnerOrAdmin()
    {
        var (_, service, company, _, employee) = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetAsync(company.Id, employee.Id, Role.Employee, null, null, null));
    }

    [Fact]
    public async Task GetAsync_CompanyWideView_OwnerSeesAllEmployees()
    {
        var (_, service, company, owner, _) = CreateSut();

        var result = await service.GetAsync(company.Id, owner.Id, Role.Owner, null, null, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAsync_ViewingAnotherEmployee_RequiresOwnerOrAdmin()
    {
        var (_, service, company, owner, employee) = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetAsync(company.Id, employee.Id, Role.Employee, owner.Id, null, null));
    }

    [Fact]
    public async Task GetAsync_OwnerViewingAnotherEmployee_Allowed()
    {
        var (_, service, company, owner, employee) = CreateSut();

        var result = await service.GetAsync(company.Id, owner.Id, Role.Owner, employee.Id, null, null);

        var dto = Assert.Single(result);
        Assert.Equal(employee.Id, dto.UserId);
    }

    [Fact]
    public async Task GetAsync_TenantIsolation_OnlyReturnsOwnCompanyUsers()
    {
        var (db, service, _, _, _) = CreateSut();
        var otherCompany = new Company { Name = "Other" };
        var otherOwner = new User { CompanyId = otherCompany.Id, Name = "OtherOwner", Email = "oo@other.tj", Role = Role.Owner };
        db.Context.Companies.Add(otherCompany);
        db.Context.Users.Add(otherOwner);
        db.Context.SaveChanges();

        var result = await service.GetAsync(otherCompany.Id, otherOwner.Id, Role.Owner, null, null, null);

        Assert.Single(result);
        Assert.Equal(otherOwner.Id, result[0].UserId);
    }
}
