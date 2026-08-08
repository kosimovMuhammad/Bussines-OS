using FluentValidation;

namespace BusinessOS.Application.ViewModels.Finance;

public record ContractDto(
    string Id,
    string Title,
    string CounterpartyName,
    string? ContactId,
    string? ContactName,
    string? DealId,
    string? DealTitle,
    string Content,
    string Status,
    DateTime? EffectiveDate,
    DateTime? ExpiryDate,
    string? TemplateId,
    DateTime? SignedAt,
    string? SignedByUserId,
    string? SignedByUserName,
    string? SignedIpAddress,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived = false);

public record CreateContractRequest(
    string Title,
    string CounterpartyName,
    string? ContactId,
    string? DealId,
    string? Content,
    string? TemplateId,
    DateTime? EffectiveDate,
    DateTime? ExpiryDate);

public record UpdateContractRequest(
    string? Title,
    string? CounterpartyName,
    string? ContactId,
    string? DealId,
    string? Content,
    ContractStatus? Status,
    DateTime? EffectiveDate,
    DateTime? ExpiryDate);

public record QueryContractsRequest(ContractStatus? Status, string? ContactId, string? DealId, bool IncludeArchived = false, int Page = 1, int Limit = 20);

public class CreateContractRequestValidator : AbstractValidator<CreateContractRequest>
{
    public CreateContractRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.CounterpartyName).NotEmpty();
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Content) || !string.IsNullOrWhiteSpace(x.TemplateId))
            .WithMessage("Either Content or TemplateId must be provided");
    }
}

public class UpdateContractRequestValidator : AbstractValidator<UpdateContractRequest>
{
    public UpdateContractRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().When(x => x.Title is not null);
        RuleFor(x => x.CounterpartyName).NotEmpty().When(x => x.CounterpartyName is not null);
        RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status is not null);
    }
}
