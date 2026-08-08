using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Named SubscriptionInvoice (not "Invoice") to avoid colliding with the existing
// Domain.Entities.Invoice (CRM deal/contact invoicing), which lives in the same flat
// entities namespace and has its own, differently-valued InvoiceStatus enum.
public class SubscriptionInvoice : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string SubscriptionId { get; set; } = string.Empty;
    public Subscription Subscription { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TJS";
    public SubscriptionInvoiceStatus Status { get; set; } = SubscriptionInvoiceStatus.Draft;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public string? PaymentProviderRef { get; set; }

    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
}
