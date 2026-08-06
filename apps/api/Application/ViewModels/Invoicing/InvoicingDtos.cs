using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Invoicing;

public record CreateInvoiceRequest(string? DealId, string ContactId, decimal Amount, string? Currency, DateTime DueDate, string? Notes);

public record UpdateInvoiceRequest(string? DealId, string? ContactId, decimal? Amount, string? Currency, DateTime? DueDate, string? Notes);

public record UpdateInvoiceStatusRequest(InvoiceStatus Status);

public record QueryInvoicesRequest(InvoiceStatus? Status, string? ContactId, string? DealId, int Page = 1, int Limit = 20);

public record CreatePaymentRequest(decimal Amount, PaymentMethod Method, DateTime? PaidAt, string? Notes);

public record InvoiceDto(
    string Id,
    string CompanyId,
    string? DealId,
    string ContactId,
    string ContactName,
    string InvoiceNumber,
    decimal Amount,
    string Currency,
    string Status,
    DateTime IssueDate,
    DateTime DueDate,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record PaymentDto(string Id, string InvoiceId, decimal Amount, string Method, DateTime PaidAt, string? Notes);

public record InvoiceDetailDto(
    string Id,
    string CompanyId,
    string? DealId,
    string ContactId,
    string ContactName,
    string InvoiceNumber,
    decimal Amount,
    string Currency,
    string Status,
    DateTime IssueDate,
    DateTime DueDate,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<PaymentDto> Payments)
    : InvoiceDto(Id, CompanyId, DealId, ContactId, ContactName, InvoiceNumber, Amount, Currency, Status, IssueDate, DueDate, Notes, CreatedAt, UpdatedAt);

public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
    }
}

public class UpdateInvoiceRequestValidator : AbstractValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceRequestValidator()
    {
        RuleFor(x => x.Amount!.Value).GreaterThanOrEqualTo(0).When(x => x.Amount is not null);
    }
}

public class UpdateInvoiceStatusRequestValidator : AbstractValidator<UpdateInvoiceStatusRequest>
{
    public UpdateInvoiceStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Method).IsInEnum();
    }
}
