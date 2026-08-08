using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Billing;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class SubscriptionInvoiceService(IAppDbContext db, IInvoicePdfRenderer pdfRenderer) : ISubscriptionInvoiceService
{
    public async Task<PagedResult<SubscriptionInvoiceDto>> ListAsync(string companyId, QuerySubscriptionInvoicesRequest query, CancellationToken cancellationToken = default)
    {
        var source = db.SubscriptionInvoices.Where(i => i.CompanyId == companyId);
        var total = await source.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await source
            .OrderByDescending(i => i.IssuedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<SubscriptionInvoiceDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<byte[]> RenderPdfAsync(string companyId, string invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await db.SubscriptionInvoices
            .Include(i => i.Company)
            .Include(i => i.Subscription).ThenInclude(s => s.Plan)
            .SingleOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Invoice not found");

        var data = new InvoicePdfData(
            invoice.Id, invoice.Company.Name, invoice.Subscription.Plan.Name, invoice.Amount, invoice.Currency,
            invoice.Status.ToString(), invoice.PeriodStart, invoice.PeriodEnd, invoice.IssuedAt, invoice.PaidAt);

        return pdfRenderer.Render(data);
    }

    private static SubscriptionInvoiceDto ToDto(SubscriptionInvoice i) => new(
        i.Id, i.Amount, i.Currency, i.Status.ToString(), i.PeriodStart, i.PeriodEnd, i.IssuedAt, i.PaidAt);
}
