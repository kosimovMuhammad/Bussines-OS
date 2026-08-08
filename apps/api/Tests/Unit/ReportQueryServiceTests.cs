using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Reports;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class ReportQueryServiceTests
{
    private static (TestDb Db, ReportQueryService Service, Company Company, User User1, User User2) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var contact = new Contact { CompanyId = company.Id, FirstName = "Client" };
        var user1 = new User { CompanyId = company.Id, Name = "U1", Email = "u1@acme.tj" };
        var user2 = new User { CompanyId = company.Id, Name = "U2", Email = "u2@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Contacts.Add(contact);
        db.Context.Users.AddRange(user1, user2);
        db.Context.SaveChanges();

        db.Context.Deals.AddRange(
            new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "D1", Stage = DealStage.Won, Value = 1000, OwnerId = user1.Id, CreatedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc) },
            new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "D2", Stage = DealStage.Won, Value = 2000, OwnerId = user1.Id, CreatedAt = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc) },
            new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "D3", Stage = DealStage.Lost, Value = 500, OwnerId = user2.Id, CreatedAt = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc) });
        db.Context.SaveChanges();

        return (db, new ReportQueryService(db.Context), company, user1, user2);
    }

    [Fact]
    public async Task RunAsync_CountWithNoGroupBy_ReturnsSingleRow()
    {
        var (_, service, company, _, _) = CreateSut();

        var result = await service.RunAsync(company.Id, "Deals", [], null, ["count"]);

        var row = Assert.Single(result.Rows);
        Assert.Null(row.GroupKey);
        Assert.Equal(3, row.Metrics["count"]);
    }

    [Fact]
    public async Task RunAsync_SumAndAvgByStage_GroupsCorrectly()
    {
        var (_, service, company, _, _) = CreateSut();

        var result = await service.RunAsync(company.Id, "Deals", [], "Stage", ["count", "sum:value", "avg:value"]);

        var won = result.Rows.Single(r => r.GroupKey == "Won");
        Assert.Equal(2, won.Metrics["count"]);
        Assert.Equal(3000, won.Metrics["sum:value"]);
        Assert.Equal(1500, won.Metrics["avg:value"]);

        var lost = result.Rows.Single(r => r.GroupKey == "Lost");
        Assert.Equal(1, lost.Metrics["count"]);
        Assert.Equal(500, lost.Metrics["sum:value"]);
    }

    [Fact]
    public async Task RunAsync_GroupByMonth_BucketsCorrectly()
    {
        var (_, service, company, _, _) = CreateSut();

        var result = await service.RunAsync(company.Id, "Deals", [], "month", ["count"]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.GroupKey == "2026-01" && r.Metrics["count"] == 2);
        Assert.Contains(result.Rows, r => r.GroupKey == "2026-02" && r.Metrics["count"] == 1);
    }

    [Fact]
    public async Task RunAsync_FilterByOwnerId_ScopesResults()
    {
        var (_, service, company, user1, _) = CreateSut();

        var result = await service.RunAsync(company.Id, "Deals", [new ReportFilterDto("OwnerId", "eq", user1.Id)], null, ["count"]);

        Assert.Equal(2, result.Rows.Single().Metrics["count"]);
    }

    [Fact]
    public async Task RunAsync_FilterByCreatedAtGte_ScopesResults()
    {
        var (_, service, company, _, _) = CreateSut();

        var result = await service.RunAsync(company.Id, "Deals", [new ReportFilterDto("CreatedAt", "gte", "2026-02-01")], null, ["count"]);

        Assert.Equal(1, result.Rows.Single().Metrics["count"]);
    }

    [Fact]
    public async Task RunAsync_UnknownEntityType_Throws()
    {
        var (_, service, company, _, _) = CreateSut();

        await Assert.ThrowsAsync<ValidationAppException>(() => service.RunAsync(company.Id, "Invoices", [], null, ["count"]));
    }

    [Fact]
    public async Task RunAsync_UnknownFilterField_Throws()
    {
        var (_, service, company, _, _) = CreateSut();

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.RunAsync(company.Id, "Deals", [new ReportFilterDto("SecretColumn", "eq", "x")], null, ["count"]));
    }

    [Fact]
    public async Task RunAsync_UnknownMetric_Throws()
    {
        var (_, service, company, _, _) = CreateSut();

        await Assert.ThrowsAsync<ValidationAppException>(() => service.RunAsync(company.Id, "Deals", [], null, ["sum:secretField"]));
    }

    [Fact]
    public async Task RunAsync_UnknownGroupBy_Throws()
    {
        var (_, service, company, _, _) = CreateSut();

        await Assert.ThrowsAsync<ValidationAppException>(() => service.RunAsync(company.Id, "Deals", [], "SecretField", ["count"]));
    }

    [Fact]
    public async Task RunAsync_TenantIsolation_OnlySeesOwnCompanyData()
    {
        var (db, service, _, _, _) = CreateSut();
        var otherCompany = new Company { Name = "Other Co" };
        db.Context.Companies.Add(otherCompany);
        db.Context.SaveChanges();

        var result = await service.RunAsync(otherCompany.Id, "Deals", [], null, ["count"]);

        Assert.Equal(0, result.Rows.Single().Metrics["count"]);
    }
}
