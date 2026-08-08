using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class InvoiceGenerationServiceTests
{
    private static (TestDb Db, InvoiceGenerationService Service, FakePaymentProvider Provider, Subscription Subscription) CreateSut(
        decimal priceMonthly = 650m, SubscriptionStatus status = SubscriptionStatus.Active)
    {
        var db = new TestDb();
        var plan = new SubscriptionPlan { Id = "plan-pro", Name = "Pro", PriceMonthly = priceMonthly, PriceYearly = priceMonthly * 10 };
        db.Context.SubscriptionPlans.Add(plan);

        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();

        var subscription = new Subscription
        {
            CompanyId = company.Id,
            PlanId = plan.Id,
            Status = status,
            BillingCycle = BillingCycle.Monthly,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-5),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(25),
        };
        db.Context.Subscriptions.Add(subscription);
        db.Context.SaveChanges();

        var provider = new FakePaymentProvider();
        var service = new InvoiceGenerationService(db.Context, provider);
        return (db, service, provider, subscription);
    }

    [Fact]
    public async Task GenerateDueInvoicesAsync_ActiveSubscriptionWithNoInvoiceYet_CreatesOne()
    {
        var (db, service, provider, subscription) = CreateSut();
        provider.NextChargeSucceeds = true;

        var generated = await service.GenerateDueInvoicesAsync();

        Assert.Equal(1, generated);
        var invoice = Assert.Single(db.Context.SubscriptionInvoices);
        Assert.Equal(subscription.Id, invoice.SubscriptionId);
        Assert.Equal(650m, invoice.Amount);
        Assert.Equal(SubscriptionInvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAt);

        var transaction = Assert.Single(db.Context.PaymentTransactions);
        Assert.Equal(PaymentTransactionStatus.Succeeded, transaction.Status);
    }

    [Fact]
    public async Task GenerateDueInvoicesAsync_ChargeFails_LeavesInvoicePendingWithFailedTransaction()
    {
        var (db, service, provider, _) = CreateSut();
        provider.NextChargeSucceeds = false;

        await service.GenerateDueInvoicesAsync();

        var invoice = Assert.Single(db.Context.SubscriptionInvoices);
        Assert.Equal(SubscriptionInvoiceStatus.Pending, invoice.Status);

        var transaction = Assert.Single(db.Context.PaymentTransactions);
        Assert.Equal(PaymentTransactionStatus.Failed, transaction.Status);
    }

    [Fact]
    public async Task GenerateDueInvoicesAsync_CalledTwiceForSamePeriod_DoesNotDuplicate()
    {
        var (db, service, _, _) = CreateSut();

        await service.GenerateDueInvoicesAsync();
        var secondRunGenerated = await service.GenerateDueInvoicesAsync();

        Assert.Equal(0, secondRunGenerated);
        Assert.Single(db.Context.SubscriptionInvoices);
    }

    [Fact]
    public async Task GenerateDueInvoicesAsync_FreePlan_SkipsWithoutCreatingInvoice()
    {
        var (db, service, _, _) = CreateSut(priceMonthly: 0m);

        var generated = await service.GenerateDueInvoicesAsync();

        Assert.Equal(0, generated);
        Assert.Empty(db.Context.SubscriptionInvoices);
    }

    [Fact]
    public async Task GenerateDueInvoicesAsync_TrialingSubscription_IsNotInvoiced()
    {
        var (db, service, _, _) = CreateSut(status: SubscriptionStatus.Trialing);

        var generated = await service.GenerateDueInvoicesAsync();

        Assert.Equal(0, generated);
        Assert.Empty(db.Context.SubscriptionInvoices);
    }
}
