using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Organization;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class EmployeeAssignmentTests
{
    private static (TestDb Db, OrganizationService Service, Company Company) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();
        return (db, new OrganizationService(db.Context), company);
    }

    private static User AddUser(TestDb db, Company company, string name)
    {
        var user = new User { CompanyId = company.Id, Name = name, Email = $"{name.ToLowerInvariant()}@acme.tj" };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        return user;
    }

    [Fact]
    public async Task UpdateAssignment_SelfAsManager_Throws()
    {
        var (db, service, company) = CreateSut();
        var alice = AddUser(db, company, "Alice");

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAssignmentAsync(company.Id, alice.Id, Role.Owner, alice.Id,
                new UpdateAssignmentRequest(null, null, alice.Id, [])));
    }

    [Fact]
    public async Task UpdateAssignment_DirectCircularChain_Throws()
    {
        var (db, service, company) = CreateSut();
        var alice = AddUser(db, company, "Alice");
        var bob = AddUser(db, company, "Bob");

        // Bob reports to Alice.
        await service.UpdateAssignmentAsync(company.Id, alice.Id, Role.Owner, bob.Id,
            new UpdateAssignmentRequest(null, null, alice.Id, []));

        // Now trying to make Alice report to Bob would create a 2-cycle.
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAssignmentAsync(company.Id, alice.Id, Role.Owner, alice.Id,
                new UpdateAssignmentRequest(null, null, bob.Id, [])));
    }

    [Fact]
    public async Task UpdateAssignment_IndirectCircularChain_Throws()
    {
        var (db, service, company) = CreateSut();
        var alice = AddUser(db, company, "Alice");
        var bob = AddUser(db, company, "Bob");
        var carol = AddUser(db, company, "Carol");

        // Carol -> Bob -> Alice
        await service.UpdateAssignmentAsync(company.Id, alice.Id, Role.Owner, bob.Id, new UpdateAssignmentRequest(null, null, alice.Id, []));
        await service.UpdateAssignmentAsync(company.Id, alice.Id, Role.Owner, carol.Id, new UpdateAssignmentRequest(null, null, bob.Id, []));

        // Alice -> Carol would close the loop: Alice -> Carol -> Bob -> Alice.
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAssignmentAsync(company.Id, alice.Id, Role.Owner, alice.Id,
                new UpdateAssignmentRequest(null, null, carol.Id, [])));
    }

    [Fact]
    public async Task UpdateAssignment_ValidManager_Succeeds()
    {
        var (db, service, company) = CreateSut();
        var alice = AddUser(db, company, "Alice");
        var bob = AddUser(db, company, "Bob");

        await service.UpdateAssignmentAsync(company.Id, alice.Id, Role.Owner, bob.Id,
            new UpdateAssignmentRequest(null, null, alice.Id, []));

        var refreshed = await db.Context.Users.FindAsync(bob.Id);
        Assert.Equal(alice.Id, refreshed!.ManagerId);
    }

    [Fact]
    public async Task UpdateAssignment_AsPlainEmployee_NotDepartmentManager_Throws()
    {
        var (db, service, company) = CreateSut();
        var employee = AddUser(db, company, "Eve");
        var target = AddUser(db, company, "Target");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateAssignmentAsync(company.Id, employee.Id, Role.Employee, target.Id,
                new UpdateAssignmentRequest(null, null, null, [])));
    }

    [Fact]
    public async Task UpdateAssignment_AsDepartmentManager_Succeeds()
    {
        var (db, service, company) = CreateSut();
        var manager = AddUser(db, company, "Manager");
        var target = AddUser(db, company, "Target");

        var department = new Department { CompanyId = company.Id, Name = "Sales", ManagerId = manager.Id };
        db.Context.Departments.Add(department);
        target.DepartmentId = department.Id;
        db.Context.SaveChanges();

        await service.UpdateAssignmentAsync(company.Id, manager.Id, Role.Employee, target.Id,
            new UpdateAssignmentRequest(department.Id, null, null, []));

        var refreshed = await db.Context.Users.FindAsync(target.Id);
        Assert.Equal(department.Id, refreshed!.DepartmentId);
    }

    [Fact]
    public async Task UpdateAssignment_ReplacesTeamMembershipsWholesale()
    {
        var (db, service, company) = CreateSut();
        var owner = AddUser(db, company, "Owner");
        var target = AddUser(db, company, "Target");

        var teamA = new Team { CompanyId = company.Id, Name = "A" };
        var teamB = new Team { CompanyId = company.Id, Name = "B" };
        db.Context.Teams.AddRange(teamA, teamB);
        db.Context.SaveChanges();

        await service.UpdateAssignmentAsync(company.Id, owner.Id, Role.Owner, target.Id,
            new UpdateAssignmentRequest(null, null, null, [teamA.Id]));
        await service.UpdateAssignmentAsync(company.Id, owner.Id, Role.Owner, target.Id,
            new UpdateAssignmentRequest(null, null, null, [teamB.Id]));

        var memberships = db.Context.TeamMembers.Where(tm => tm.UserId == target.Id).ToList();
        Assert.Single(memberships);
        Assert.Equal(teamB.Id, memberships[0].TeamId);
    }
}
