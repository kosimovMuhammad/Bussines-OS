using BusinessOS.Application.ViewModels.Analytics;

namespace BusinessOS.Application.Services;

public interface IAnalyticsService
{
    Task<DashboardOverviewDto> GetOverviewAsync(string companyId, CancellationToken cancellationToken = default);
    Task<SalesAnalyticsDto> GetSalesAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RevenuePointDto>> GetRevenueAsync(string companyId, CancellationToken cancellationToken = default);
    Task<TaskStatsDto> GetTaskStatsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityFeedItemDto>> GetActivityFeedAsync(string companyId, CancellationToken cancellationToken = default);
    Task<FinanceAnalyticsDto> GetFinanceAsync(string companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UpcomingMeetingDto>> GetUpcomingMeetingsAsync(string companyId, string userId, CancellationToken cancellationToken = default);
    Task<ForecastDto> GetForecastAsync(string companyId, CancellationToken cancellationToken = default);
}
