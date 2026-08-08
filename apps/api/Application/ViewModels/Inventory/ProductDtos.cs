using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Inventory;

public record ProductDto(
    string Id,
    string Name,
    string Sku,
    string? Description,
    decimal Price,
    decimal Cost,
    string Currency,
    string? CategoryId,
    string? CategoryName,
    string Unit,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived = false);

public record CreateProductRequest(
    string Name,
    string Sku,
    string? Description,
    decimal Price = 0,
    decimal Cost = 0,
    string Currency = "TJS",
    string? CategoryId = null,
    ProductUnit Unit = ProductUnit.Piece,
    bool IsActive = true);

public record UpdateProductRequest(
    string? Name,
    string? Sku,
    string? Description,
    decimal? Price,
    decimal? Cost,
    string? Currency,
    string? CategoryId,
    ProductUnit? Unit,
    bool? IsActive);

public record QueryProductsRequest(string? Search, string? CategoryId, bool? IsActive, bool IncludeArchived = false, int Page = 1, int Limit = 20);

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Sku).NotEmpty();
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Unit).IsInEnum();
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Sku).NotEmpty().When(x => x.Sku is not null);
        RuleFor(x => x.Price!.Value).GreaterThanOrEqualTo(0).When(x => x.Price is not null);
        RuleFor(x => x.Cost!.Value).GreaterThanOrEqualTo(0).When(x => x.Cost is not null);
        RuleFor(x => x.Unit!.Value).IsInEnum().When(x => x.Unit is not null);
    }
}
