using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Invoicing;

namespace BusinessOS.Application.Services;

public interface IInvoicingService
{
    Task<PagedResult<InvoiceDto>> ListAsync(string companyId, QueryInvoicesRequest query, CancellationToken cancellationToken = default);
    Task<InvoiceDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<InvoiceDto> CreateAsync(string companyId, CreateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceDto> UpdateAsync(string companyId, string id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceDto> UpdateStatusAsync(string companyId, string id, UpdateInvoiceStatusRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentDto>> ListPaymentsAsync(string companyId, string invoiceId, CancellationToken cancellationToken = default);
    Task<PaymentDto> AddPaymentAsync(string companyId, string invoiceId, CreatePaymentRequest request, CancellationToken cancellationToken = default);
}
