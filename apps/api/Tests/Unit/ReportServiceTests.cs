using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Reports;
using BusinessOS.Domain.Entities;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class ReportServiceTests
{
    private static (TestDb Db, ReportService Service, Company Company, User User) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var service = new ReportService(db.Context, new ReportQueryService(db.Context));
        return (db, service, company, user);
    }

    private static CreateReportRequest ValidRequest(string name = "Deals by stage") =>
        new(name, "Deals", [], "Stage", ["count"]);

    [Fact]
    public async Task CreateAsync_ValidConfig_PersistsAndReturnsDto()
    {
        var (db, service, company, user) = CreateSut();

        var dto = await service.CreateAsync(company.Id, user.Id, ValidRequest());

        Assert.Equal("Deals by stage", dto.Name);
        Assert.Equal("Deals", dto.EntityType);
        Assert.Single(db.Context.Reports);
    }

    [Fact]
    public async Task CreateAsync_UnknownEntityType_ThrowsAndDoesNotPersist()
    {
        var (db, service, company, user) = CreateSut();

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.CreateAsync(company.Id, user.Id, new CreateReportRequest("Bad", "Invoices", [], null, ["count"])));

        Assert.Empty(db.Context.Reports);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_Throws()
    {
        var (_, service, company, user) = CreateSut();

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.CreateAsync(company.Id, user.Id, ValidRequest(name: "  ")));
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyOwnCompanysReports()
    {
        var (db, service, company, user) = CreateSut();
        await service.CreateAsync(company.Id, user.Id, ValidRequest());

        var otherCompany = new Company { Name = "Other" };
        db.Context.Companies.Add(otherCompany);
        db.Context.SaveChanges();
        var otherUser = new User { CompanyId = otherCompany.Id, Name = "Bob", Email = "bob@other.tj" };
        db.Context.Users.Add(otherUser);
        db.Context.SaveChanges();
        await service.CreateAsync(otherCompany.Id, otherUser.Id, ValidRequest("Other's report"));

        var listA = await service.ListAsync(company.Id);
        var listB = await service.ListAsync(otherCompany.Id);

        Assert.Single(listA);
        Assert.Equal("Deals by stage", listA[0].Name);
        Assert.Single(listB);
        Assert.Equal("Other's report", listB[0].Name);
    }

    [Fact]
    public async Task RunAsync_ExecutesStoredConfig()
    {
        var (_, service, company, user) = CreateSut();
        var created = await service.CreateAsync(company.Id, user.Id, ValidRequest());

        var result = await service.RunAsync(company.Id, created.Id);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task RunAsync_CrossTenantId_ThrowsNotFound()
    {
        var (db, service, company, user) = CreateSut();
        var created = await service.CreateAsync(company.Id, user.Id, ValidRequest());

        var otherCompany = new Company { Name = "Other" };
        db.Context.Companies.Add(otherCompany);
        db.Context.SaveChanges();

        await Assert.ThrowsAsync<NotFoundException>(() => service.RunAsync(otherCompany.Id, created.Id));
    }

    [Fact]
    public async Task DeleteAsync_RemovesReport()
    {
        var (db, service, company, user) = CreateSut();
        var created = await service.CreateAsync(company.Id, user.Id, ValidRequest());

        await service.DeleteAsync(company.Id, created.Id);

        Assert.Empty(db.Context.Reports);
    }

    [Fact]
    public async Task DeleteAsync_CrossTenantId_ThrowsNotFoundAndDoesNotDelete()
    {
        var (db, service, company, user) = CreateSut();
        var created = await service.CreateAsync(company.Id, user.Id, ValidRequest());

        var otherCompany = new Company { Name = "Other" };
        db.Context.Companies.Add(otherCompany);
        db.Context.SaveChanges();

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(otherCompany.Id, created.Id));
        Assert.Single(db.Context.Reports);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ThrowsNotFound()
    {
        var (_, service, company, _) = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(company.Id, "no-such-id"));
    }
}
