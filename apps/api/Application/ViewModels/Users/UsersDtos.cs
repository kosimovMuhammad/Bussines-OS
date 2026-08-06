using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Users;

public record UpdateUserRequest(string? Name, string? AvatarUrl, UserStatus? Status);

public record InviteUserRequest(string Name, string Email, Role? Role);

public record UpdateRoleRequest(Role Role);

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
