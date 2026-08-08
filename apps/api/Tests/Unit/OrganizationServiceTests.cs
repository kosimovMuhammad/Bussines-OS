using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Organization;
using BusinessOS.Domain.Entities;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class OrganizationServiceTests
{
    [Fact]
    public async Task CreateDepartment_TopLevel_Succeeds()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var dto = await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Sales", null, null, null));

        Assert.Equal("Sales", dto.Name);
        Assert.Null(dto.ParentDepartmentId);
    }

    [Fact]
    public async Task CreateDepartment_DuplicateNameAtSameLevel_Throws()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Sales", null, null, null));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Sales", null, null, null)));
    }

    [Fact]
    public async Task CreateDepartment_SameNameUnderDifferentParents_Succeeds()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var parentA = await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Region A", null, null, null));
        var parentB = await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Region B", null, null, null));

        await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Sales", null, parentA.Id, null));
        var second = await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Sales", null, parentB.Id, null));

        Assert.Equal("Sales", second.Name);
    }

    [Fact]
    public async Task RemoveDepartment_WithActiveEmployees_Throws()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var dept = await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Sales", null, null, null));
        db.Context.Users.Add(new User { CompanyId = company.Id, Name = "Bob", Email = "bob@acme.tj", DepartmentId = dept.Id });
        db.Context.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => service.RemoveDepartmentAsync(company.Id, dept.Id));
    }

    [Fact]
    public async Task RemoveDepartment_WithSubDepartments_Throws()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var parent = await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Parent", null, null, null));
        await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Child", null, parent.Id, null));

        await Assert.ThrowsAsync<ConflictException>(() => service.RemoveDepartmentAsync(company.Id, parent.Id));
    }

    [Fact]
    public async Task RemoveDepartment_Empty_Succeeds()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var dept = await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Sales", null, null, null));
        await service.RemoveDepartmentAsync(company.Id, dept.Id);

        var remaining = await service.ListDepartmentsFlatAsync(company.Id);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task ListDepartmentsTree_NestsChildrenUnderParent()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var parent = await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Parent", null, null, null));
        await service.CreateDepartmentAsync(company.Id, new CreateDepartmentRequest("Child", null, parent.Id, null));

        var tree = await service.ListDepartmentsTreeAsync(company.Id);

        var root = Assert.Single(tree);
        Assert.Equal("Parent", root.Name);
        var child = Assert.Single(root.Children);
        Assert.Equal("Child", child.Name);
    }

    [Fact]
    public async Task CreateBranch_SecondHeadquarters_UnsetsFirst()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var first = await service.CreateBranchAsync(company.Id, new CreateBranchRequest("HQ 1", null, null, null, null, true));
        var second = await service.CreateBranchAsync(company.Id, new CreateBranchRequest("HQ 2", null, null, null, null, true));

        var refreshedFirst = await service.GetBranchAsync(company.Id, first.Id);
        Assert.False(refreshedFirst.IsHeadquarters);
        Assert.True(second.IsHeadquarters);
    }

    [Fact]
    public async Task RemoveBranch_WithAssignedUsers_Throws()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var branch = await service.CreateBranchAsync(company.Id, new CreateBranchRequest("Dushanbe", null, null, null, null, false));
        db.Context.Users.Add(new User { CompanyId = company.Id, Name = "Bob", Email = "bob@acme.tj", BranchId = branch.Id });
        db.Context.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => service.RemoveBranchAsync(company.Id, branch.Id));
    }

    [Fact]
    public async Task AddTeamMember_Duplicate_Throws()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var team = await service.CreateTeamAsync(company.Id, new CreateTeamRequest(null, "Hunters", null, null));
        var user = new User { CompanyId = company.Id, Name = "Bob", Email = "bob@acme.tj" };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        await service.AddTeamMemberAsync(company.Id, team.Id, new AddTeamMemberRequest(user.Id));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddTeamMemberAsync(company.Id, team.Id, new AddTeamMemberRequest(user.Id)));
    }

    [Fact]
    public async Task RemoveTeamMember_ThenListMembers_IsEmpty()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);
        var company = Seed(db);

        var team = await service.CreateTeamAsync(company.Id, new CreateTeamRequest(null, "Hunters", null, null));
        var user = new User { CompanyId = company.Id, Name = "Bob", Email = "bob@acme.tj" };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        await service.AddTeamMemberAsync(company.Id, team.Id, new AddTeamMemberRequest(user.Id));
        await service.RemoveTeamMemberAsync(company.Id, team.Id, user.Id);

        var members = await service.ListTeamMembersAsync(company.Id, team.Id);
        Assert.Empty(members);
    }

    [Fact]
    public async Task ListDepartmentsFlat_IsScopedToCurrentTenant()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);

        var companyA = new Company { Name = "A" };
        var companyB = new Company { Name = "B" };
        db.Context.Companies.AddRange(companyA, companyB);
        db.Context.SaveChanges();

        await service.CreateDepartmentAsync(companyA.Id, new CreateDepartmentRequest("A-Dept", null, null, null));
        await service.CreateDepartmentAsync(companyB.Id, new CreateDepartmentRequest("B-Dept", null, null, null));

        db.TenantContext.Set(companyA.Id);
        var visible = await service.ListDepartmentsFlatAsync(companyA.Id);

        Assert.Single(visible);
        Assert.Equal("A-Dept", visible[0].Name);
    }

    private static Company Seed(TestDb db)
    {
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();
        return company;
    }
}
