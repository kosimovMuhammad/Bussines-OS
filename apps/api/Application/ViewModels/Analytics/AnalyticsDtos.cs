namespace BusinessOS.Application.ViewModels.Analytics;

public record DashboardOverviewDto(int ContactsCount, int OpenTasksCount, IReadOnlyDictionary<string, int> DealsByStage, int TodayTimeTrackedSec);

public record SalesAnalyticsDto(IReadOnlyDictionary<string, int> PipelineByStage, decimal WinRate);

public record RevenuePointDto(string Period, decimal Revenue);

public record TaskStatsDto(IReadOnlyDictionary<string, int> ByStatus, IReadOnlyDictionary<string, int> ByPriority, int OverdueCount);

public record ActivityFeedItemDto(string Type, string Id, string Title, DateTime Timestamp);

public record FinanceAnalyticsDto(decimal Outstanding, decimal Overdue, decimal PaidThisMonth, IReadOnlyList<RevenuePointDto> RevenueByMonth);

public record UpcomingMeetingDto(string Id, string Title, DateTime StartAt, DateTime EndAt, string? ContactName);

public record ForecastDto(decimal WeightedForecast30Days, string Summary);
