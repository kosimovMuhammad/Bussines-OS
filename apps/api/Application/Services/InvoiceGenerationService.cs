using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class InvoiceGenerationService(IAppDbContext db, IPaymentProvider paymentProvider) : IInvoiceGenerationService
{
    public async Task<int> GenerateDueInvoicesAsync(CancellationToken cancellationToken = default)
    {
        var subscriptions = await db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue)
            .ToListAsync(cancellationToken);

        var generated = 0;
        foreach (var subscription in subscriptions)
        {
            var alreadyInvoiced = await db.SubscriptionInvoices.AnyAsync(
                i => i.SubscriptionId == subscription.Id && i.PeriodStart == subscription.CurrentPeriodStart, cancellationToken);
            if (alreadyInvoiced)
            {
                continue;
            }

            var amount = subscription.BillingCycle == BillingCycle.Monthly ? subscription.Plan.PriceMonthly : subscription.Plan.PriceYearly;
            if (amount <= 0)
            {
                // Free plan - nothing to bill, no invoice row needed.
                continue;
            }

            var invoice = new SubscriptionInvoice
            {
                CompanyId = subscription.CompanyId,
                SubscriptionId = subscription.Id,
                Amount = amount,
                Currency = subscription.Plan.Currency,
                Status = SubscriptionInvoiceStatus.Pending,
                PeriodStart = subscription.CurrentPeriodStart,
                PeriodEnd = subscription.CurrentPeriodEnd,
            };
            db.SubscriptionInvoices.Add(invoice);
            await db.SaveChangesAsync(cancellationToken);

            var chargeResult = await paymentProvider.ChargeAsync(
                new ChargeRequest(subscription.CompanyId, invoice.Id, amount, invoice.Currency), cancellationToken);

            db.PaymentTransactions.Add(new PaymentTransaction
            {
                CompanyId = subscription.CompanyId,
                SubscriptionInvoiceId = invoice.Id,
                Provider = paymentProvider.ProviderName,
                ProviderTransactionId = chargeResult.ProviderTransactionId,
                Amount = amount,
                Currency = invoice.Currency,
                Status = chargeResult.Success ? PaymentTransactionStatus.Succeeded : PaymentTransactionStatus.Failed,
                CompletedAt = chargeResult.Success ? DateTime.UtcNow : null,
            });

            if (chargeResult.Success)
            {
                invoice.Status = SubscriptionInvoiceStatus.Paid;
                invoice.PaidAt = DateTime.UtcNow;
                invoice.PaymentProviderRef = chargeResult.ProviderTransactionId;
            }

            await db.SaveChangesAsync(cancellationToken);
            generated++;
        }

        return generated;
    }
}
