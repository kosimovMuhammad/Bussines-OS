using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class OrgChartTests
{
    [Fact]
    public async Task GetOrgChart_NestsChildDepartmentsAndGroupsEmployees()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);

        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();

        var parent = new Department { CompanyId = company.Id, Name = "Sales" };
        var child = new Department { CompanyId = company.Id, Name = "Sales EU", ParentDepartmentId = parent.Id };
        db.Context.Departments.AddRange(parent, child);

        var deptEmployee = new User { CompanyId = company.Id, Name = "Bob", Email = "bob@acme.tj", DepartmentId = parent.Id };
        var unassignedEmployee = new User { CompanyId = company.Id, Name = "Carol", Email = "carol@acme.tj" };
        db.Context.Users.AddRange(deptEmployee, unassignedEmployee);
        db.Context.SaveChanges();

        var chart = await service.GetOrgChartAsync(company.Id);

        var root = Assert.Single(chart.Departments);
        Assert.Equal("Sales", root.Name);
        Assert.Single(root.Employees);
        Assert.Equal("Bob", root.Employees[0].Name);
        var childNode = Assert.Single(root.Children);
        Assert.Equal("Sales EU", childNode.Name);
        Assert.Empty(childNode.Employees);
        Assert.Single(chart.UnassignedEmployees);
        Assert.Equal("Carol", chart.UnassignedEmployees[0].Name);
    }

    [Fact]
    public async Task GetUserOrgChain_ReturnsManagementChainAndDirectReports()
    {
        using var db = new TestDb();
        var service = new OrganizationService(db.Context);

        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();

        var ceo = new User { CompanyId = company.Id, Name = "CEO", Email = "ceo@acme.tj" };
        db.Context.Users.Add(ceo);
        db.Context.SaveChanges();

        var manager = new User { CompanyId = company.Id, Name = "Manager", Email = "manager@acme.tj", ManagerId = ceo.Id };
        db.Context.Users.Add(manager);
        db.Context.SaveChanges();

        var report = new User { CompanyId = company.Id, Name = "Report", Email = "report@acme.tj", ManagerId = manager.Id };
        db.Context.Users.Add(report);
        db.Context.SaveChanges();

        var chain = await service.GetUserOrgChainAsync(company.Id, manager.Id);

        Assert.Equal("Manager", chain.User.Name);
        var chainManager = Assert.Single(chain.ManagementChain);
        Assert.Equal("CEO", chainManager.Name);
        var directReport = Assert.Single(chain.DirectReports);
        Assert.Equal("Report", directReport.Name);
    }
}
