using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Inventory;

public record PurchaseOrderLineItemDto(string Id, string ProductId, string ProductName, string ProductSku, int Quantity, decimal UnitCost, decimal LineTotal);

public record PurchaseOrderDto(
    string Id,
    string SupplierId,
    string SupplierName,
    string OrderNumber,
    string Status,
    DateTime OrderDate,
    DateTime? ExpectedDate,
    DateTime? ReceivedDate,
    string Currency,
    string? Notes,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived = false);

public record PurchaseOrderDetailDto(
    string Id,
    string SupplierId,
    string SupplierName,
    string OrderNumber,
    string Status,
    DateTime OrderDate,
    DateTime? ExpectedDate,
    DateTime? ReceivedDate,
    string Currency,
    string? Notes,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<PurchaseOrderLineItemDto> LineItems,
    bool IsArchived = false)
    : PurchaseOrderDto(Id, SupplierId, SupplierName, OrderNumber, Status, OrderDate, ExpectedDate, ReceivedDate, Currency, Notes, TotalAmount, CreatedAt, UpdatedAt, IsArchived);

public record CreatePurchaseOrderLineItemRequest(string ProductId, int Quantity, decimal UnitCost);

public record CreatePurchaseOrderRequest(
    string SupplierId,
    DateTime? ExpectedDate,
    string Currency,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderLineItemRequest> LineItems);

public record UpdatePurchaseOrderRequest(
    string? SupplierId,
    PurchaseOrderStatus? Status,
    DateTime? ExpectedDate,
    string? Currency,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderLineItemRequest>? LineItems);

public record QueryPurchaseOrdersRequest(PurchaseOrderStatus? Status, string? SupplierId, bool IncludeArchived = false, int Page = 1, int Limit = 20);

public class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty();
        RuleFor(x => x.LineItems).NotEmpty().WithMessage("A purchase order needs at least one line item");
        RuleForEach(x => x.LineItems).ChildRules(li =>
        {
            li.RuleFor(i => i.ProductId).NotEmpty();
            li.RuleFor(i => i.Quantity).GreaterThan(0);
            li.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0);
        });
    }
}

public class UpdatePurchaseOrderRequestValidator : AbstractValidator<UpdatePurchaseOrderRequest>
{
    public UpdatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Currency).NotEmpty().When(x => x.Currency is not null);
        RuleFor(x => x.LineItems).NotEmpty().WithMessage("A purchase order needs at least one line item").When(x => x.LineItems is not null);
        RuleForEach(x => x.LineItems).ChildRules(li =>
        {
            li.RuleFor(i => i.ProductId).NotEmpty();
            li.RuleFor(i => i.Quantity).GreaterThan(0);
            li.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0);
        }).When(x => x.LineItems is not null);
    }
}
