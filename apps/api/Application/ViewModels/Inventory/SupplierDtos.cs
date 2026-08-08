using FluentValidation;

namespace BusinessOS.Application.ViewModels.Inventory;

public record SupplierDto(
    string Id,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived = false);

public record CreateSupplierRequest(
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? Notes,
    bool IsActive = true);

public record UpdateSupplierRequest(
    string? Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? Notes,
    bool? IsActive);

public record QuerySuppliersRequest(string? Search, bool? IsActive, bool IncludeArchived = false, int Page = 1, int Limit = 20);

public class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
