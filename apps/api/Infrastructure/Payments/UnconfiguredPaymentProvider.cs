using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Infrastructure.Payments;

// Placeholder IPaymentProvider registered until a real CIS-friendly processor is chosen.
// Charges always report "not configured" instead of throwing, so invoice generation
// still runs and leaves invoices in Pending state truthfully reflecting that nothing has
// actually been charged; the webhook endpoint likewise reports nothing recognized.
// Swapping in a real provider later is a single DI registration change, nothing above
// this class needs to move.
public class UnconfiguredPaymentProvider : IPaymentProvider
{
    public string ProviderName => "unconfigured";

    public Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChargeResult(false, null, "No payment provider is configured yet"));

    public Task<WebhookEvent> ParseWebhookAsync(string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebhookEvent(false, null, PaymentTransactionStatus.Pending));
}
