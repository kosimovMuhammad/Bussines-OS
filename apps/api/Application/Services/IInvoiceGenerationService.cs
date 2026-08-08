namespace BusinessOS.Application.Services;

public interface IInvoiceGenerationService
{
    // Ensures every Active/PastDue subscription has a SubscriptionInvoice covering its
    // CURRENT billing period, generating one (and attempting a charge through
    // IPaymentProvider) if missing. Idempotent by (SubscriptionId, PeriodStart) - safe to
    // call repeatedly, e.g. on every scheduler tick. Returns how many invoices were
    // generated in this call.
    Task<int> GenerateDueInvoicesAsync(CancellationToken cancellationToken = default);
}
