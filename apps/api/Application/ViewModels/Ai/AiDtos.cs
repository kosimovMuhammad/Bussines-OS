using FluentValidation;

namespace BusinessOS.Application.ViewModels.Ai;

public record SummarizeRequest(string CommunicationId);

public record AiQueryRequest(string Question);

public record AiDraftEmailRequest(string ContactId, string? Context);

public record QuotationItemRequest(string Description, decimal Quantity, decimal UnitPrice);

public record AiGenerateQuotationRequest(string DealId, List<QuotationItemRequest> Items);

public record QueuedResponse(bool Queued, string Id);

public record AiSummaryDto(string Id, string CommunicationId, string Summary, string? Sentiment, string? ActionItemsJson, DateTime? NextFollowUp, DateTime GeneratedAt);

public record AiQueryResultDto(string Id, string Question, string? Intent, string ResultType, string Status, string? ResultRowsJson, string? Answer, string? ErrorMessage);

public record AiEmailDraftDto(string Id, string ContactId, string Status, string? Subject, string? Body, string? ErrorMessage);

public record AiQuotationDto(string Id, string DealId, string ItemsJson, decimal? TotalAmount, string Status, string? DownloadUrl, string? ErrorMessage);

// Structured payloads serialized into AiQueryResult.ResultRowsJson when Question maps to a
// known intent - lets the frontend render a dedicated widget instead of raw text, keyed off
// AiQueryResultDto.ResultType.
public record AiClientContactDto(string Id, string Name, DateTime? LastContactDate);

public record AiClientListResultDto(int ClientCount, decimal PotentialRevenue, string Currency, IReadOnlyList<AiClientContactDto> Contacts);

public record AiSalesRankingEntryDto(string UserId, string Name, int DealsWon, decimal TotalValue);

public record AiSalesRankingResultDto(IReadOnlyList<AiSalesRankingEntryDto> Ranking);

public record AiForecastRequest(string? Period);

public record TopExpectedPaymentDto(string ContactId, string ContactName, decimal Amount);

public record AiForecastDto(decimal EstimatedAmount, string Currency, double ConfidencePercent, string Period, IReadOnlyList<TopExpectedPaymentDto> TopExpectedPayments);

public record AiUsageBreakdownDto(string Endpoint, int RequestCount, int TokensUsed, decimal Cost);

public record AiUsageStatsDto(DateTime PeriodStart, DateTime PeriodEnd, int TotalRequests, int TotalTokensUsed, decimal EstimatedCost, IReadOnlyList<AiUsageBreakdownDto> ByEndpoint);

public record AiAssistantMessageRequest(string? ConversationId, string Message);

public record AiAssistantSendResultDto(string ConversationId, string UserMessageId, string AssistantMessageId);

public record AiAssistantMessageDto(string Id, string Role, string Content, string Status, DateTime CreatedAt);

public record AiAssistantConversationDto(string Id, string? Title, DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<AiAssistantMessageDto> Messages);

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

public class AiForecastRequestValidator : AbstractValidator<AiForecastRequest>
{
    public AiForecastRequestValidator() =>
        RuleFor(x => x.Period)
            .Matches(@"^\d{4}-(0[1-9]|1[0-2])$").WithMessage("Period must be in 'yyyy-MM' format")
            .When(x => x.Period is not null);
}

public class AiAssistantMessageRequestValidator : AbstractValidator<AiAssistantMessageRequest>
{
    public AiAssistantMessageRequestValidator() => RuleFor(x => x.Message).NotEmpty();
}
