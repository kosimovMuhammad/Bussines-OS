using FluentValidation;

namespace BusinessOS.Application.ViewModels.Ai;

public record SummarizeRequest(string CommunicationId);

public record AiQueryRequest(string Question);

public record AiDraftEmailRequest(string ContactId, string? Context);

public record QuotationItemRequest(string Description, decimal Quantity, decimal UnitPrice);

public record AiGenerateQuotationRequest(string DealId, List<QuotationItemRequest> Items);

public record QueuedResponse(bool Queued, string Id);

public record AiSummaryDto(string Id, string CommunicationId, string Summary, string? Sentiment, string? ActionItemsJson, DateTime? NextFollowUp, DateTime GeneratedAt);

public record AiQueryResultDto(string Id, string Question, string? Intent, string Status, string? ResultRowsJson, string? Answer, string? ErrorMessage);

public record AiEmailDraftDto(string Id, string ContactId, string Status, string? Subject, string? Body, string? ErrorMessage);

public record AiQuotationDto(string Id, string DealId, string ItemsJson, decimal? TotalAmount, string Status, string? DownloadUrl, string? ErrorMessage);

public class SummarizeRequestValidator : AbstractValidator<SummarizeRequest>
{
    public SummarizeRequestValidator() => RuleFor(x => x.CommunicationId).NotEmpty();
}

public class AiQueryRequestValidator : AbstractValidator<AiQueryRequest>
{
    public AiQueryRequestValidator() => RuleFor(x => x.Question).NotEmpty();
}

public class AiDraftEmailRequestValidator : AbstractValidator<AiDraftEmailRequest>
{
    public AiDraftEmailRequestValidator() => RuleFor(x => x.ContactId).NotEmpty();
}

public class AiGenerateQuotationRequestValidator : AbstractValidator<AiGenerateQuotationRequest>
{
    public AiGenerateQuotationRequestValidator()
    {
        RuleFor(x => x.DealId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
