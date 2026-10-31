using BusinessOS.Application.ViewModels.Ai;

namespace BusinessOS.Application.Services;

public interface IAiUsageService
{
    Task RecordAsync(string companyId, string? userId, string endpoint, int requestCount = 1, int? tokensUsed = null, CancellationToken cancellationToken = default);
    Task<AiUsageStatsDto> GetUsageAsync(string companyId, CancellationToken cancellationToken = default);
}
