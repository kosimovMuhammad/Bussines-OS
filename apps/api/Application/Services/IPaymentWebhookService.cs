namespace BusinessOS.Application.Services;

public interface IPaymentWebhookService
{
    // Returns true if the event was recognized and (idempotently) applied. Signature
    // verification, if the eventual provider needs it, lives inside
    // IPaymentProvider.ParseWebhookAsync - this service only does the recognized-event ->
    // PaymentTransaction/SubscriptionInvoice bookkeeping.
    Task<bool> HandleAsync(string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
}
