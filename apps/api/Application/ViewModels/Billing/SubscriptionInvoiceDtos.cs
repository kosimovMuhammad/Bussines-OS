namespace BusinessOS.Application.ViewModels.Billing;

public record SubscriptionInvoiceDto(
    string Id,
    decimal Amount,
    string Currency,
    string Status,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime IssuedAt,
    DateTime? PaidAt);

public record QuerySubscriptionInvoicesRequest(int Page = 1, int Limit = 20);
