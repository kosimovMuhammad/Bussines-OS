using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class PaymentWebhookServiceTests
{
    private static (TestDb Db, PaymentWebhookService Service, FakePaymentProvider Provider, PaymentTransaction Transaction, SubscriptionInvoice Invoice) CreateSut()
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
            Status = SubscriptionInvoiceStatus.Pending,
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd,
        };
        db.Context.SubscriptionInvoices.Add(invoice);
        db.Context.SaveChanges();

        var transaction = new PaymentTransaction
        {
            CompanyId = company.Id,
            SubscriptionInvoiceId = invoice.Id,
            Provider = "fake",
            ProviderTransactionId = "txn-1",
            Amount = 650m,
            Status = PaymentTransactionStatus.Pending,
        };
        db.Context.PaymentTransactions.Add(transaction);
        db.Context.SaveChanges();

        var provider = new FakePaymentProvider();
        var service = new PaymentWebhookService(db.Context, provider);
        return (db, service, provider, transaction, invoice);
    }

    [Fact]
    public async Task HandleAsync_RecognizedSuccessEvent_MarksTransactionAndInvoicePaid()
    {
        var (db, service, provider, transaction, invoice) = CreateSut();
        provider.NextWebhookEvent = new WebhookEvent(true, transaction.ProviderTransactionId, PaymentTransactionStatus.Succeeded);

        var applied = await service.HandleAsync("{}", new Dictionary<string, string>());

        Assert.True(applied);
        Assert.Equal(PaymentTransactionStatus.Succeeded, transaction.Status);
        Assert.Equal(SubscriptionInvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAt);
    }

    [Fact]
    public async Task HandleAsync_RedeliveredEventWithSameStatus_IsIdempotent()
    {
        var (db, service, provider, transaction, invoice) = CreateSut();
        provider.NextWebhookEvent = new WebhookEvent(true, transaction.ProviderTransactionId, PaymentTransactionStatus.Succeeded);

        await service.HandleAsync("{}", new Dictionary<string, string>());
        var paidAtAfterFirst = invoice.PaidAt;

        var appliedAgain = await service.HandleAsync("{}", new Dictionary<string, string>());

        Assert.True(appliedAgain);
        Assert.Equal(paidAtAfterFirst, invoice.PaidAt);
    }

    [Fact]
    public async Task HandleAsync_UnrecognizedEvent_ReturnsFalseAndChangesNothing()
    {
        var (db, service, provider, transaction, _) = CreateSut();
        provider.NextWebhookEvent = new WebhookEvent(false, null, PaymentTransactionStatus.Pending);

        var applied = await service.HandleAsync("{}", new Dictionary<string, string>());

        Assert.False(applied);
        Assert.Equal(PaymentTransactionStatus.Pending, transaction.Status);
    }

    [Fact]
    public async Task HandleAsync_UnknownProviderTransactionId_ReturnsFalse()
    {
        var (db, service, provider, _, _) = CreateSut();
        provider.NextWebhookEvent = new WebhookEvent(true, "no-such-txn", PaymentTransactionStatus.Succeeded);

        var applied = await service.HandleAsync("{}", new Dictionary<string, string>());

        Assert.False(applied);
    }
}
