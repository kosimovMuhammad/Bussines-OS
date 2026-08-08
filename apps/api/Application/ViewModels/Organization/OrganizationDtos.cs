using FluentValidation;

namespace BusinessOS.Application.ViewModels.Organization;

public record DepartmentDto(
    string Id,
    string Name,
    string? Description,
    string? ParentDepartmentId,
    string? ManagerId,
    string? ManagerName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record DepartmentTreeNode(
    string Id,
    string Name,
    string? Description,
    string? ManagerId,
    string? ManagerName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<DepartmentTreeNode> Children);

public record CreateDepartmentRequest(string Name, string? Description, string? ParentDepartmentId, string? ManagerId);

public record UpdateDepartmentRequest(string? Name, string? Description, string? ParentDepartmentId, string? ManagerId);

public record TeamDto(
    string Id,
    string? DepartmentId,
    string Name,
    string? Description,
    string? TeamLeadId,
    string? TeamLeadName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateTeamRequest(string? DepartmentId, string Name, string? Description, string? TeamLeadId);

public record UpdateTeamRequest(string? DepartmentId, string? Name, string? Description, string? TeamLeadId);

public record TeamMemberDto(string UserId, string UserName, DateTime JoinedAt);

public record AddTeamMemberRequest(string UserId);

public record BranchDto(
    string Id,
    string Name,
    string? Address,
    string? City,
    string? Country,
    string Timezone,
    bool IsHeadquarters,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateBranchRequest(string Name, string? Address, string? City, string? Country, string? Timezone, bool IsHeadquarters = false);

public record UpdateBranchRequest(string? Name, string? Address, string? City, string? Country, string? Timezone, bool? IsHeadquarters);

public class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
    }
}

public class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public class UpdateTeamRequestValidator : AbstractValidator<UpdateTeamRequest>
{
    public UpdateTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
    }
}

public class AddTeamMemberRequestValidator : AbstractValidator<AddTeamMemberRequest>
{
    public AddTeamMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public class UpdateBranchRequestValidator : AbstractValidator<UpdateBranchRequest>
{
    public UpdateBranchRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
    }
}
