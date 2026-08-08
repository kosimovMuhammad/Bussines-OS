using FluentValidation;

namespace BusinessOS.Application.ViewModels;

// Shared shape across every bulk-update/bulk-delete endpoint (Contacts/Deals/Tasks) - each
// id is processed through that module's existing single-entity Update/Remove method inside
// its own try/catch, so a bad id reports as a failed row instead of rolling back everyone
// else's successful changes (per the Phase 6 spec: no all-or-nothing convention already
// exists in this codebase to match, so per-id partial success is the right default).
public record BulkOperationResultItem(string Id, bool Success, string? Error);

public record BulkOperationResult(int SuccessCount, int FailureCount, IReadOnlyList<BulkOperationResultItem> Results)
{
    public static BulkOperationResult From(IReadOnlyList<BulkOperationResultItem> results) =>
        new(results.Count(r => r.Success), results.Count(r => !r.Success), results);
}

public record BulkDeleteRequest(List<string> Ids);

public class BulkDeleteRequestValidator : AbstractValidator<BulkDeleteRequest>
{
    public BulkDeleteRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(ids => ids.Count <= 200).WithMessage("A bulk operation is limited to 200 ids at a time");
    }
}
