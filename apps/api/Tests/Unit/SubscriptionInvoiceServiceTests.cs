using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class SubscriptionInvoiceServiceTests
{
    private static (TestDb Db, SubscriptionInvoiceService Service, Company Company, SubscriptionInvoice Invoice) CreateSut()
    {
        var db = new TestDb();
        var plan = new SubscriptionPlan { Id = "plan-pro", Name = "Pro", PriceMonthly = 650m };
        db.Context.SubscriptionPlans.Add(plan);

        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();

        var subscription = new Subscription
        {
            CompanyId = company.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-5),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(25),
        };
        db.Context.Subscriptions.Add(subscription);
        db.Context.SaveChanges();

        var invoice = new SubscriptionInvoice
        {
            CompanyId = company.Id,
            SubscriptionId = subscription.Id,
            Amount = 650m,
            Status = SubscriptionInvoiceStatus.Paid,
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd,
        };
        db.Context.SubscriptionInvoices.Add(invoice);
        db.Context.SaveChanges();

        var service = new SubscriptionInvoiceService(db.Context, new FakeInvoicePdfRenderer());
        return (db, service, company, invoice);
    }

    [Fact]
    public async Task ListAsync_ReturnsCompanysInvoices()
    {
        var (_, service, company, invoice) = CreateSut();

        var result = await service.ListAsync(company.Id, new QuerySubscriptionInvoicesRequest());

        var dto = Assert.Single(result.Data);
        Assert.Equal(invoice.Id, dto.Id);
        Assert.Equal(650m, dto.Amount);
    }

    [Fact]
    public async Task RenderPdfAsync_UnknownInvoice_Throws()
    {
        var (_, service, company, _) = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => service.RenderPdfAsync(company.Id, "no-such-invoice"));
    }

    [Fact]
    public async Task RenderPdfAsync_KnownInvoice_ReturnsBytes()
    {
        var (_, service, company, invoice) = CreateSut();

        var bytes = await service.RenderPdfAsync(company.Id, invoice.Id);

        Assert.NotEmpty(bytes);
    }
}

public class FakeInvoicePdfRenderer : BusinessOS.Application.Abstraction.IInvoicePdfRenderer
{
    public byte[] Render(BusinessOS.Application.Abstraction.InvoicePdfData data) => [1, 2, 3];
}
