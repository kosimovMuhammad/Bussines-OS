using BusinessOS.Application.ViewModels.Ai;

namespace BusinessOS.Application.Services;

public interface IAiService
{
    Task<QueuedResponse> SummarizeAsync(string companyId, string userId, SummarizeRequest request, CancellationToken cancellationToken = default);
    Task<AiSummaryDto> GetSummaryAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<QueuedResponse> QueryAsync(string companyId, string userId, AiQueryRequest request, CancellationToken cancellationToken = default);
    Task<AiQueryResultDto> GetQueryResultAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<QueuedResponse> DraftEmailAsync(string companyId, string userId, AiDraftEmailRequest request, CancellationToken cancellationToken = default);
    Task<AiEmailDraftDto> GetDraftEmailAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<QueuedResponse> GenerateQuotationAsync(string companyId, string userId, AiGenerateQuotationRequest request, CancellationToken cancellationToken = default);
    Task<AiQuotationDto> GetQuotationAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<AiForecastDto> GetForecastAsync(string companyId, string userId, AiForecastRequest request, CancellationToken cancellationToken = default);
    Task<AiAssistantSendResultDto> SendAssistantMessageAsync(string companyId, string userId, AiAssistantMessageRequest request, CancellationToken cancellationToken = default);
    Task<AiAssistantConversationDto> GetAssistantConversationAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task ProcessSummarizeAsync(string communicationId, CancellationToken cancellationToken = default);
    Task ProcessQueryAsync(string aiQueryResultId, CancellationToken cancellationToken = default);
    Task ProcessDraftEmailAsync(string aiEmailDraftId, CancellationToken cancellationToken = default);
    Task ProcessQuotationAsync(string aiQuotationId, CancellationToken cancellationToken = default);
    Task ProcessAssistantMessageAsync(string assistantMessageId, CancellationToken cancellationToken = default);
}
