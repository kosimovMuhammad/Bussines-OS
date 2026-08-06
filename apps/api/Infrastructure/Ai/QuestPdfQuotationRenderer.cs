using BusinessOS.Application.Abstraction;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BusinessOS.Infrastructure.Ai;

public class QuestPdfQuotationRenderer : IQuotationPdfRenderer
{
    static QuestPdfQuotationRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(string dealTitle, IReadOnlyList<QuotationLineItem> items, decimal total, string currency)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text($"Quotation - {dealTitle}").FontSize(18).Bold();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Description").Bold();
                        header.Cell().Text("Qty").Bold();
                        header.Cell().Text("Unit Price").Bold();
                        header.Cell().Text("Total").Bold();
                    });

                    foreach (var item in items)
                    {
                        table.Cell().Text(item.Description);
                        table.Cell().Text(item.Quantity.ToString("0.##"));
                        table.Cell().Text($"{item.UnitPrice:0.##} {currency}");
                        table.Cell().Text($"{item.Quantity * item.UnitPrice:0.##} {currency}");
                    }
                });

                page.Footer().AlignRight().Text($"Total: {total:0.##} {currency}").FontSize(14).Bold();
            });
        });

        return document.GeneratePdf();
    }
}
