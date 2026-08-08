using FluentValidation;

namespace BusinessOS.Application.ViewModels.Inventory;

public record InventoryDto(
    string Id,
    string ProductId,
    string ProductName,
    string ProductSku,
    string? BranchId,
    string? BranchName,
    int Quantity,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record InventoryMovementDto(
    string Id,
    string Direction,
    int Quantity,
    string Reason,
    string? Reference,
    string? UserId,
    string? UserName,
    DateTime CreatedAt);

public record InventoryDetailDto(
    string Id,
    string ProductId,
    string ProductName,
    string ProductSku,
    string? BranchId,
    string? BranchName,
    int Quantity,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<InventoryMovementDto> RecentMovements)
    : InventoryDto(Id, ProductId, ProductName, ProductSku, BranchId, BranchName, Quantity, CreatedAt, UpdatedAt);

public record CreateInventoryRequest(string ProductId, string? BranchId, int Quantity = 0);

public record UpdateInventoryRequest(int? Quantity);

public record QueryInventoryRequest(string? ProductId, string? BranchId, int Page = 1, int Limit = 20);

// Direction is implied by which endpoint (stock-in vs stock-out) receives this, so Quantity
// here is always a positive magnitude, never signed.
public record StockMovementRequest(string ProductId, string? BranchId, int Quantity, string? Reason, string? Reference);

public class CreateInventoryRequestValidator : AbstractValidator<CreateInventoryRequest>
{
    public CreateInventoryRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
    }
}

public class UpdateInventoryRequestValidator : AbstractValidator<UpdateInventoryRequest>
{
    public UpdateInventoryRequestValidator()
    {
        RuleFor(x => x.Quantity!.Value).GreaterThanOrEqualTo(0).When(x => x.Quantity is not null);
    }
}

public class StockMovementRequestValidator : AbstractValidator<StockMovementRequest>
{
    public StockMovementRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
