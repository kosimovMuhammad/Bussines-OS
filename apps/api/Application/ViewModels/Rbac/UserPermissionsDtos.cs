using FluentValidation;

namespace BusinessOS.Application.ViewModels.Rbac;

public record EffectivePermissionsDto(
    string UserId,
    string? RoleId,
    string? RoleName,
    IReadOnlyList<string> PermissionKeys,
    IReadOnlyList<UserPermissionOverrideDto> Overrides);

public record UserPermissionOverrideDto(string PermissionId, string PermissionKey, bool Allow);

public record UserPermissionOverrideItem(string PermissionId, bool Allow);

// Full replace, not a delta - the override set becomes exactly this list (empty clears every
// override and falls back to the role's permissions).
public record UpdateUserPermissionsRequest(List<UserPermissionOverrideItem> Overrides);

public class UpdateUserPermissionsRequestValidator : AbstractValidator<UpdateUserPermissionsRequest>
{
    public UpdateUserPermissionsRequestValidator()
    {
        RuleForEach(x => x.Overrides).ChildRules(item => item.RuleFor(i => i.PermissionId).NotEmpty());
    }
}
