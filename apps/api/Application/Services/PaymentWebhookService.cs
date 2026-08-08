using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class PaymentWebhookService(IAppDbContext db, IPaymentProvider paymentProvider) : IPaymentWebhookService
{
    public async Task<bool> HandleAsync(string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        var evt = await paymentProvider.ParseWebhookAsync(rawPayload, headers, cancellationToken);
        if (!evt.Recognized || evt.ProviderTransactionId is null)
        {
            return false;
        }

        var transaction = await db.PaymentTransactions
            .Include(t => t.SubscriptionInvoice)
            .SingleOrDefaultAsync(t => t.ProviderTransactionId == evt.ProviderTransactionId, cancellationToken);
        if (transaction is null)
        {
            return false;
        }

        // Idempotent no-op on webhook redelivery - the provider's ProviderTransactionId
        // unique index (Phase 1) is what makes this lookup safe/exact.
        if (transaction.Status == evt.Status)
        {
            return true;
        }

        transaction.Status = evt.Status;
        if (evt.Status is PaymentTransactionStatus.Succeeded or PaymentTransactionStatus.Failed)
        {
            transaction.CompletedAt = DateTime.UtcNow;
        }

        if (transaction.SubscriptionInvoice is not null)
        {
            if (evt.Status == PaymentTransactionStatus.Succeeded)
            {
                transaction.SubscriptionInvoice.Status = SubscriptionInvoiceStatus.Paid;
                transaction.SubscriptionInvoice.PaidAt = DateTime.UtcNow;
                transaction.SubscriptionInvoice.PaymentProviderRef = transaction.ProviderTransactionId;
            }
            else if (evt.Status == PaymentTransactionStatus.Failed)
            {
                transaction.SubscriptionInvoice.Status = SubscriptionInvoiceStatus.Failed;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
