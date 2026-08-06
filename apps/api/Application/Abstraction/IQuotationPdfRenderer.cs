namespace BusinessOS.Application.Abstraction;

public record QuotationLineItem(string Description, decimal Quantity, decimal UnitPrice);

public interface IQuotationPdfRenderer
{
    byte[] Render(string dealTitle, IReadOnlyList<QuotationLineItem> items, decimal total, string currency);
}
