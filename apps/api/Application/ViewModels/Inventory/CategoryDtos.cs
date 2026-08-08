using FluentValidation;

namespace BusinessOS.Application.ViewModels.Inventory;

public record CategoryDto(
    string Id,
    string Name,
    string? Description,
    string? ParentCategoryId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived = false);

public record CreateCategoryRequest(string Name, string? Description, string? ParentCategoryId);

public record UpdateCategoryRequest(string? Name, string? Description, string? ParentCategoryId);

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
    }
}
