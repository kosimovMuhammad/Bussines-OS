using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Invoicing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class InvoicingService(IAppDbContext db) : IInvoicingService
{
    public async Task<PagedResult<InvoiceDto>> ListAsync(string companyId, QueryInvoicesRequest query, CancellationToken cancellationToken = default)
    {
        var invoices = db.Invoices.Include(i => i.Contact).AsQueryable();

        if (query.Status is not null) invoices = invoices.Where(i => i.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.ContactId)) invoices = invoices.Where(i => i.ContactId == query.ContactId);
        if (!string.IsNullOrWhiteSpace(query.DealId)) invoices = invoices.Where(i => i.DealId == query.DealId);

        var total = await invoices.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await invoices
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<InvoiceDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.Include(i => i.Contact).Include(i => i.Payments)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found");

        return new InvoiceDetailDto(
            invoice.Id, invoice.CompanyId, invoice.DealId, invoice.ContactId,
            $"{invoice.Contact.FirstName} {invoice.Contact.LastName}".Trim(), invoice.InvoiceNumber, invoice.Amount,
            invoice.Currency, invoice.Status.ToString(), invoice.IssueDate, invoice.DueDate, invoice.Notes,
            invoice.CreatedAt, invoice.UpdatedAt, invoice.Payments.Select(ToPaymentDto).ToList());
    }

    public async Task<InvoiceDto> CreateAsync(string companyId, CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        if (request.DealId is not null)
        {
            var dealExists = await db.Deals.AnyAsync(d => d.Id == request.DealId, cancellationToken);
            if (!dealExists) throw new NotFoundException("Deal not found");
        }

        var invoice = new Invoice
        {
            CompanyId = companyId,
            DealId = request.DealId,
            ContactId = request.ContactId,
            Amount = request.Amount,
            Currency = request.Currency ?? "TJS",
            DueDate = request.DueDate,
            Notes = request.Notes,
        };

        const int maxAttempts = 10;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            invoice.InvoiceNumber = await NextInvoiceNumberAsync(companyId, cancellationToken);
            db.Invoices.Add(invoice);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                invoice.Contact = contact;
                return ToDto(invoice);
            }
            catch (DbUpdateException) when (attempt < maxAttempts - 1)
            {
                db.Invoices.Remove(invoice);
            }
        }

        throw new ConflictException("Could not generate a unique invoice number");
    }

    public async Task<InvoiceDto> UpdateAsync(string companyId, string id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.Include(i => i.Contact).SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found");

        if (request.DealId is not null)
        {
            var dealExists = await db.Deals.AnyAsync(d => d.Id == request.DealId, cancellationToken);
            if (!dealExists) throw new NotFoundException("Deal not found");
            invoice.DealId = request.DealId;
        }

        if (request.ContactId is not null)
        {
            var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
                ?? throw new NotFoundException("Contact not found");
            invoice.ContactId = request.ContactId;
            invoice.Contact = contact;
        }

        if (request.Amount is not null) invoice.Amount = request.Amount.Value;
        if (request.Currency is not null) invoice.Currency = request.Currency;
        if (request.DueDate is not null) invoice.DueDate = request.DueDate.Value;
        if (request.Notes is not null) invoice.Notes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(invoice);
    }

    public async Task<InvoiceDto> UpdateStatusAsync(string companyId, string id, UpdateInvoiceStatusRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.Include(i => i.Contact).SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found");

        invoice.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(invoice);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found");

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new ConflictException("Only draft invoices can be deleted");
        }

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentDto>> ListPaymentsAsync(string companyId, string invoiceId, CancellationToken cancellationToken = default)
    {
        var exists = await db.Invoices.AnyAsync(i => i.Id == invoiceId, cancellationToken);
        if (!exists) throw new NotFoundException("Invoice not found");

        var payments = await db.Payments.Where(p => p.InvoiceId == invoiceId).OrderByDescending(p => p.PaidAt).ToListAsync(cancellationToken);
        return payments.Select(ToPaymentDto).ToList();
    }

    public async Task<PaymentDto> AddPaymentAsync(string companyId, string invoiceId, CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.Include(i => i.Payments).SingleOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException("Invoice not found");

        var payment = new Payment
        {
            CompanyId = companyId,
            InvoiceId = invoiceId,
            Amount = request.Amount,
            Method = request.Method,
            PaidAt = request.PaidAt ?? DateTime.UtcNow,
            Notes = request.Notes,
        };

        db.Payments.Add(payment);

        var totalPaid = invoice.Payments.Sum(p => p.Amount) + request.Amount;
        if (totalPaid >= invoice.Amount)
        {
            invoice.Status = InvoiceStatus.Paid;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToPaymentDto(payment);
    }

    private async Task<string> NextInvoiceNumberAsync(string companyId, CancellationToken cancellationToken)
    {
        var count = await db.Invoices.CountAsync(i => i.CompanyId == companyId, cancellationToken);
        return $"INV-{(count + 1):D4}";
    }

    private static PaymentDto ToPaymentDto(Payment p) => new(p.Id, p.InvoiceId, p.Amount, p.Method.ToString(), p.PaidAt, p.Notes);

    private static InvoiceDto ToDto(Invoice i) => new(
        i.Id, i.CompanyId, i.DealId, i.ContactId, $"{i.Contact.FirstName} {i.Contact.LastName}".Trim(),
        i.InvoiceNumber, i.Amount, i.Currency, i.Status.ToString(), i.IssueDate, i.DueDate, i.Notes, i.CreatedAt, i.UpdatedAt);
}
