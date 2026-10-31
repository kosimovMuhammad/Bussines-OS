using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels.Ai;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

// Flat per-request rate table stands in for real Gemini token-cost metering - IGeminiClient
// doesn't currently surface token usage from the API response, so cost is estimated per
// endpoint rather than computed from actual tokens. TokensUsed is still recorded on the log
// row whenever a caller has it, so swapping in real metering later is additive.
public class AiUsageService(IAppDbContext db) : IAiUsageService
{
    private static readonly IReadOnlyDictionary<string, decimal> CostPerRequest = new Dictionary<string, decimal>
    {
        [AiUsageEndpoints.Summarize] = 0.002m,
        [AiUsageEndpoints.Query] = 0.004m,
        [AiUsageEndpoints.DraftEmail] = 0.003m,
        [AiUsageEndpoints.GenerateQuotation] = 0.003m,
        [AiUsageEndpoints.Forecast] = 0.001m,
        [AiUsageEndpoints.Assistant] = 0.003m,
    };

    public async Task RecordAsync(string companyId, string? userId, string endpoint, int requestCount = 1, int? tokensUsed = null, CancellationToken cancellationToken = default)
    {
        db.AiUsageLogs.Add(new AiUsageLog
        {
            CompanyId = companyId,
            UserId = userId,
            Endpoint = endpoint,
            RequestCount = requestCount,
            TokensUsed = tokensUsed,
            Cost = CostPerRequest.GetValueOrDefault(endpoint, 0m) * requestCount,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AiUsageStatsDto> GetUsageAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var subscription = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);

        var periodStart = subscription?.CurrentPeriodStart
            ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = subscription?.CurrentPeriodEnd ?? periodStart.AddMonths(1);

        var byEndpoint = await db.AiUsageLogs
            .Where(l => l.CompanyId == companyId && l.CreatedAt >= periodStart && l.CreatedAt < periodEnd)
            .GroupBy(l => l.Endpoint)
            .Select(g => new AiUsageBreakdownDto(g.Key, g.Sum(l => l.RequestCount), g.Sum(l => l.TokensUsed ?? 0), g.Sum(l => l.Cost)))
            .ToListAsync(cancellationToken);

        return new AiUsageStatsDto(
            periodStart,
            periodEnd,
            byEndpoint.Sum(e => e.RequestCount),
            byEndpoint.Sum(e => e.TokensUsed),
            byEndpoint.Sum(e => e.Cost),
            byEndpoint);
    }
}
