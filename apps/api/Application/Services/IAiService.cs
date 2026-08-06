using BusinessOS.Application.ViewModels.Ai;

namespace BusinessOS.Application.Services;

public interface IAiService
{
    Task<QueuedResponse> SummarizeAsync(string companyId, SummarizeRequest request, CancellationToken cancellationToken = default);
    Task<AiSummaryDto> GetSummaryAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<QueuedResponse> QueryAsync(string companyId, AiQueryRequest request, CancellationToken cancellationToken = default);
    Task<AiQueryResultDto> GetQueryResultAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<QueuedResponse> DraftEmailAsync(string companyId, AiDraftEmailRequest request, CancellationToken cancellationToken = default);
    Task<AiEmailDraftDto> GetDraftEmailAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<QueuedResponse> GenerateQuotationAsync(string companyId, string userId, AiGenerateQuotationRequest request, CancellationToken cancellationToken = default);
    Task<AiQuotationDto> GetQuotationAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task ProcessSummarizeAsync(string communicationId, CancellationToken cancellationToken = default);
    Task ProcessQueryAsync(string aiQueryResultId, CancellationToken cancellationToken = default);
    Task ProcessDraftEmailAsync(string aiEmailDraftId, CancellationToken cancellationToken = default);
    Task ProcessQuotationAsync(string aiQuotationId, CancellationToken cancellationToken = default);
}
