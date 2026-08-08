using FluentValidation;

namespace BusinessOS.Application.ViewModels.Finance;

public record ExpenseDto(
    string Id,
    string Description,
    decimal Amount,
    string Currency,
    string Category,
    DateTime Date,
    string PaidById,
    string PaidByName,
    string? BranchId,
    string? BranchName,
    string? ReceiptFileId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived = false);

public record CreateExpenseRequest(
    string Description,
    decimal Amount,
    string Currency,
    string Category,
    DateTime Date,
    string PaidById,
    string? BranchId,
    string? ReceiptFileId);

public record UpdateExpenseRequest(
    string? Description,
    decimal? Amount,
    string? Currency,
    string? Category,
    DateTime? Date,
    string? PaidById,
    string? BranchId,
    string? ReceiptFileId);

public record QueryExpensesRequest(string? Category, string? PaidById, string? BranchId, DateTime? From, DateTime? To, bool IncludeArchived = false, int Page = 1, int Limit = 20);

public class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty();
        RuleFor(x => x.Category).NotEmpty();
        RuleFor(x => x.PaidById).NotEmpty();
    }
}

public class UpdateExpenseRequestValidator : AbstractValidator<UpdateExpenseRequest>
{
    public UpdateExpenseRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().When(x => x.Description is not null);
        RuleFor(x => x.Amount!.Value).GreaterThan(0).When(x => x.Amount is not null);
        RuleFor(x => x.Currency).NotEmpty().When(x => x.Currency is not null);
        RuleFor(x => x.Category).NotEmpty().When(x => x.Category is not null);
    }
}
