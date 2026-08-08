using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Billing;

namespace BusinessOS.Application.Services;

public interface ISubscriptionInvoiceService
{
    Task<PagedResult<SubscriptionInvoiceDto>> ListAsync(string companyId, QuerySubscriptionInvoicesRequest query, CancellationToken cancellationToken = default);

    Task<byte[]> RenderPdfAsync(string companyId, string invoiceId, CancellationToken cancellationToken = default);
}
