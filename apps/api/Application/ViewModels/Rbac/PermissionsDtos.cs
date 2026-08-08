using FluentValidation;

namespace BusinessOS.Application.ViewModels.Rbac;

public record PermissionDto(string Id, string Key, string Module, string? Description);

public record CreatePermissionRequest(string Key, string Module, string? Description);

public record UpdatePermissionRequest(string? Description);

public class CreatePermissionRequestValidator : AbstractValidator<CreatePermissionRequest>
{
    public CreatePermissionRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Module).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdatePermissionRequestValidator : AbstractValidator<UpdatePermissionRequest>
{
    public UpdatePermissionRequestValidator()
    {
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
