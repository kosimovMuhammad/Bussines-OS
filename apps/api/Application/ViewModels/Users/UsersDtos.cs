using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Users;

public record UpdateUserRequest(string? Name, string? AvatarUrl, UserStatus? Status);

// RbacRoleId optionally assigns a custom RBAC role in one shot; when omitted, the invited
// user's role is resolved from the legacy Role via SystemRoles.ForLegacyRole (see UsersService).
public record InviteUserRequest(string Name, string Email, Role? Role, string? RbacRoleId = null);

public record UpdateRoleRequest(Role Role, string? RbacRoleId = null);

// For a user who was invited and logged in with an email code and has no password yet.
// Distinct from ChangePasswordRequest, which requires proving knowledge of the current one.
public record SetPasswordRequest(string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
    }
}

public class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role).IsInEnum().When(x => x.Role is not null);
    }
}

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class SetPasswordRequestValidator : AbstractValidator<SetPasswordRequest>
{
    public SetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public record BulkInviteUsersRequest(List<InviteUserRequest> Users);

public record BulkInviteResultItem(string Email, bool Success, string? Error, string? UserId);

public record BulkInviteResult(int SuccessCount, int FailureCount, IReadOnlyList<BulkInviteResultItem> Results)
{
    public static BulkInviteResult From(IReadOnlyList<BulkInviteResultItem> results) =>
        new(results.Count(r => r.Success), results.Count(r => !r.Success), results);
}

public class BulkInviteUsersRequestValidator : AbstractValidator<BulkInviteUsersRequest>
{
    public BulkInviteUsersRequestValidator()
    {
        RuleFor(x => x.Users).NotEmpty().Must(u => u.Count <= 200).WithMessage("A bulk operation is limited to 200 users at a time");
        RuleForEach(x => x.Users).SetValidator(new InviteUserRequestValidator());
    }
}
