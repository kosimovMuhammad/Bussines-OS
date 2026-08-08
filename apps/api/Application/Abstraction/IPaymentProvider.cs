using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Abstraction;

public record ChargeRequest(string CompanyId, string SubscriptionInvoiceId, decimal Amount, string Currency);

public record ChargeResult(bool Success, string? ProviderTransactionId, string? FailureReason);

public record WebhookEvent(bool Recognized, string? ProviderTransactionId, PaymentTransactionStatus Status);

// Provider-agnostic on purpose - concrete provider selection (Payme/Click/Alif Bank or
// another CIS-friendly processor; explicitly NOT Stripe, which doesn't support
// Tajikistan/CIS payouts) is deferred until the user confirms it. Everything above this
// abstraction (invoice generation, the webhook endpoint) is real and wired; only the
// registered implementation is a placeholder (see Infrastructure/Payments) until then.
public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default);

    Task<WebhookEvent> ParseWebhookAsync(string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
}
