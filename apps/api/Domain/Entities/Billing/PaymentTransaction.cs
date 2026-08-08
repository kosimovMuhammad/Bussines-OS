using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class PaymentTransaction : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? SubscriptionInvoiceId { get; set; }
    public SubscriptionInvoice? SubscriptionInvoice { get; set; }
    public string Provider { get; set; } = string.Empty;

    // Nullable - a transaction row is created when payment is initiated, before the
    // provider assigns/returns its own transaction id (e.g. via webhook).
    public string? ProviderTransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TJS";
    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
