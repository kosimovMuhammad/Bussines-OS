using FluentValidation;

namespace BusinessOS.Application.ViewModels.Billing;

public record SubscriptionPlanFeatureDto(string FeatureKey, int? Limit, bool Enabled);

public record SubscriptionPlanDto(
    string Id,
    string Name,
    string? Description,
    decimal PriceMonthly,
    decimal PriceYearly,
    string Currency,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<SubscriptionPlanFeatureDto> Features);

public record CreateSubscriptionPlanFeatureRequest(string FeatureKey, int? Limit, bool Enabled);

public record CreateSubscriptionPlanRequest(
    string Name,
    string? Description,
    decimal PriceMonthly,
    decimal PriceYearly,
    string? Currency,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<CreateSubscriptionPlanFeatureRequest> Features);

public record UpdateSubscriptionPlanRequest(
    string? Name,
    string? Description,
    decimal? PriceMonthly,
    decimal? PriceYearly,
    string? Currency,
    bool? IsActive,
    int? SortOrder,
    IReadOnlyList<CreateSubscriptionPlanFeatureRequest>? Features);

public class CreateSubscriptionPlanRequestValidator : AbstractValidator<CreateSubscriptionPlanRequest>
{
    public CreateSubscriptionPlanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.PriceMonthly).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PriceYearly).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Features).ChildRules(f => f.RuleFor(i => i.FeatureKey).NotEmpty());
    }
}

public class UpdateSubscriptionPlanRequestValidator : AbstractValidator<UpdateSubscriptionPlanRequest>
{
    public UpdateSubscriptionPlanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null);
        RuleFor(x => x.PriceMonthly).GreaterThanOrEqualTo(0).When(x => x.PriceMonthly is not null);
        RuleFor(x => x.PriceYearly).GreaterThanOrEqualTo(0).When(x => x.PriceYearly is not null);
    }
}
