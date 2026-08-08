using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Deals;

public record CreateDealRequest(
    string ContactId,
    string Title,
    decimal Value = 0,
    string Currency = "TJS",
    DealStage Stage = DealStage.Lead,
    int? Probability = null,
    DateTime? ExpectedCloseDate = null,
    string? OwnerId = null);

public record UpdateDealRequest(
    string? ContactId,
    string? Title,
    decimal? Value,
    string? Currency,
    int? Probability,
    DateTime? ExpectedCloseDate,
    string? OwnerId);

public record UpdateDealStageRequest(DealStage Stage);

public record QueryDealsRequest(DealStage? Stage, string? OwnerId, bool IncludeArchived = false);

public record DealDto(
    string Id,
    string CompanyId,
    string ContactId,
    string ContactName,
    string Title,
    decimal Value,
    string Currency,
    string Stage,
    int Probability,
    DateTime? ExpectedCloseDate,
    string? OwnerId,
    string? OwnerName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived = false);

public record BulkUpdateDealsRequest(List<string> Ids, UpdateDealRequest Patch);

public record DealTaskSummary(string Id, string Title, string Status, DateTime? DueDate);

public record DealDetailDto(
    string Id,
    string CompanyId,
    string ContactId,
    string ContactName,
    string Title,
    decimal Value,
    string Currency,
    string Stage,
    int Probability,
    DateTime? ExpectedCloseDate,
    string? OwnerId,
    string? OwnerName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<DealTaskSummary> Tasks,
    bool IsArchived = false)
    : DealDto(Id, CompanyId, ContactId, ContactName, Title, Value, Currency, Stage, Probability, ExpectedCloseDate, OwnerId, OwnerName, CreatedAt, UpdatedAt, IsArchived);

public class CreateDealRequestValidator : AbstractValidator<CreateDealRequest>
{
    public CreateDealRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Probability!.Value).InclusiveBetween(0, 100).When(x => x.Probability is not null);
    }
}

public class UpdateDealRequestValidator : AbstractValidator<UpdateDealRequest>
{
    public UpdateDealRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().When(x => x.Title is not null);
        RuleFor(x => x.Value!.Value).GreaterThanOrEqualTo(0).When(x => x.Value is not null);
        RuleFor(x => x.Probability!.Value).InclusiveBetween(0, 100).When(x => x.Probability is not null);
    }
}

public class UpdateDealStageRequestValidator : AbstractValidator<UpdateDealStageRequest>
{
    public UpdateDealStageRequestValidator()
    {
        RuleFor(x => x.Stage).IsInEnum();
    }
}

public class BulkUpdateDealsRequestValidator : AbstractValidator<BulkUpdateDealsRequest>
{
    public BulkUpdateDealsRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(ids => ids.Count <= 200).WithMessage("A bulk operation is limited to 200 ids at a time");
        RuleFor(x => x.Patch).SetValidator(new UpdateDealRequestValidator());
    }
}
