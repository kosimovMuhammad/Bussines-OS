namespace BusinessOS.Application.Abstraction;

public record InvoicePdfData(
    string InvoiceId,
    string CompanyName,
    string PlanName,
    decimal Amount,
    string Currency,
    string Status,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime IssuedAt,
    DateTime? PaidAt);

public interface IInvoicePdfRenderer
{
    byte[] Render(InvoicePdfData data);
}
