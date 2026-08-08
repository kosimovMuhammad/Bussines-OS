using FluentValidation;

namespace BusinessOS.Application.ViewModels.Organization;

public record UpdateAssignmentRequest(string? DepartmentId, string? BranchId, string? ManagerId, IReadOnlyList<string> TeamIds);

public class UpdateAssignmentRequestValidator : AbstractValidator<UpdateAssignmentRequest>
{
    public UpdateAssignmentRequestValidator()
    {
        RuleFor(x => x.TeamIds).NotNull();
    }
}
