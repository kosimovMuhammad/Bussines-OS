using FluentValidation;

namespace BusinessOS.Application.ViewModels.Rbac;

public record RoleDto(
    string Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<PermissionDto> Permissions,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateRoleRequest(string Name, string? Description, List<string>? PermissionIds);

// Named to avoid colliding with ViewModels.Users.UpdateRoleRequest, which patches a user's
// legacy Role enum - this patches an RbacRole row's own Name/Description.
public record UpdateRoleDetailsRequest(string? Name, string? Description);

public record AttachPermissionRequest(string PermissionId);

public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateRoleDetailsRequestValidator : AbstractValidator<UpdateRoleDetailsRequest>
{
    public UpdateRoleDetailsRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class AttachPermissionRequestValidator : AbstractValidator<AttachPermissionRequest>
{
    public AttachPermissionRequestValidator()
    {
        RuleFor(x => x.PermissionId).NotEmpty();
    }
}
