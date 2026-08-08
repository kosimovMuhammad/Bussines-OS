using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class RevenueReportServiceTests
{
    private static (TestDb Db, RevenueReportService Service, Company Company) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var contact = new Contact { CompanyId = company.Id, FirstName = "Client" };
        db.Context.Companies.Add(company);
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();

        var now = DateTime.UtcNow;
        db.Context.Deals.AddRange(
            new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Won-this-period", Stage = DealStage.Won, Value = 1000, CreatedAt = now.AddDays(-10), UpdatedAt = now.AddDays(-2) },
            new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Won-prev-period", Stage = DealStage.Won, Value = 500, CreatedAt = now.AddDays(-35), UpdatedAt = now.AddDays(-30) },
            new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Open1", Stage = DealStage.Proposal, Probability = 50, Value = 2000, CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-3) },
            new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Lost1", Stage = DealStage.Lost, Value = 300, CreatedAt = now.AddDays(-5), UpdatedAt = now.AddDays(-1) });
        db.Context.SaveChanges();

        return (db, new RevenueReportService(db.Context), company);
    }

    [Fact]
    public async Task GetAsync_TotalRevenue_OnlyCountsWonDealsInPeriod()
    {
        var (_, service, company) = CreateSut();
        var from = DateTime.UtcNow.AddDays(-20);
        var to = DateTime.UtcNow;

        var result = await service.GetAsync(company.Id, from, to, "month");

        Assert.Equal(1000, result.TotalRevenue);
    }

    [Fact]
    public async Task GetAsync_ForecastedRevenue_WeightsOpenDealsByProbability()
    {
        var (_, service, company) = CreateSut();

        var result = await service.GetAsync(company.Id, DateTime.UtcNow.AddDays(-20), DateTime.UtcNow, "month");

        Assert.Equal(1000, result.ForecastedRevenue);
    }

    [Fact]
    public async Task GetAsync_PreviousPeriodComparison_ComputesPercentChange()
    {
        var (_, service, company) = CreateSut();
        var to = DateTime.UtcNow;
        var from = to.AddDays(-20);

        var result = await service.GetAsync(company.Id, from, to, "month");

        Assert.Equal(500, result.PreviousPeriodRevenue);
        Assert.NotNull(result.PercentChange);
        Assert.Equal(100.0, result.PercentChange!.Value, 1);
    }

    [Fact]
    public async Task GetAsync_ByStage_BreaksDownByPipelineStage()
    {
        var (_, service, company) = CreateSut();

        var result = await service.GetAsync(company.Id, DateTime.UtcNow.AddDays(-20), DateTime.UtcNow, "month");

        Assert.Contains(result.ByStage, s => s.Stage == "Won" && s.Value == 1000);
        Assert.Contains(result.ByStage, s => s.Stage == "Proposal" && s.Value == 2000);
        Assert.Contains(result.ByStage, s => s.Stage == "Lost" && s.Value == 300);
    }

    [Fact]
    public async Task GetAsync_InvalidGroupBy_Throws()
    {
        var (_, service, company) = CreateSut();

        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetAsync(company.Id, null, null, "fortnight"));
    }

    [Fact]
    public async Task GetAsync_TenantIsolation_OnlySeesOwnCompanyDeals()
    {
        var (db, service, _) = CreateSut();
        var otherCompany = new Company { Name = "Other" };
        db.Context.Companies.Add(otherCompany);
        db.Context.SaveChanges();

        var result = await service.GetAsync(otherCompany.Id, DateTime.UtcNow.AddDays(-20), DateTime.UtcNow, "month");

        Assert.Equal(0, result.TotalRevenue);
        Assert.Equal(0, result.ForecastedRevenue);
    }
}
