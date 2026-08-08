using BusinessOS.Application.Abstraction;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BusinessOS.Infrastructure.Billing;

public class QuestPdfInvoiceRenderer : IInvoicePdfRenderer
{
    static QuestPdfInvoiceRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(InvoicePdfData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text($"Invoice - {data.InvoiceId}").FontSize(18).Bold();

                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text($"Billed to: {data.CompanyName}");
                    column.Item().Text($"Plan: {data.PlanName}");
                    column.Item().Text($"Period: {data.PeriodStart:yyyy-MM-dd} - {data.PeriodEnd:yyyy-MM-dd}");
                    column.Item().Text($"Issued: {data.IssuedAt:yyyy-MM-dd}");
                    column.Item().Text($"Status: {data.Status}");
                    if (data.PaidAt is not null)
                    {
                        column.Item().Text($"Paid: {data.PaidAt:yyyy-MM-dd}");
                    }
                });

                page.Footer().AlignRight().Text($"Total: {data.Amount:0.##} {data.Currency}").FontSize(14).Bold();
            });
        });

        return document.GeneratePdf();
    }
}
