namespace BusinessOS.Application.ViewModels.Analytics;

public record DashboardOverviewDto(int ContactsCount, int OpenTasksCount, IReadOnlyDictionary<string, int> DealsByStage, int TodayTimeTrackedSec);

public record SalesAnalyticsDto(IReadOnlyDictionary<string, int> PipelineByStage, decimal WinRate);

public record RevenuePointDto(string Period, decimal Revenue);

public record RevenueAnalyticsDto(decimal TotalWonValue, IReadOnlyList<RevenuePointDto> RevenueByMonth);

public record TaskStatsDto(IReadOnlyDictionary<string, int> ByStatus, IReadOnlyDictionary<string, int> ByPriority, int OverdueCount);

public record ActivityFeedItemDataDto(string Id, string Title);

public record ActivityFeedItemDto(string Type, DateTime Timestamp, ActivityFeedItemDataDto Data);

public record FinanceAnalyticsDto(decimal TotalOutstanding, decimal Overdue, decimal PaidThisMonth, IReadOnlyList<RevenuePointDto> RevenueByMonth);

public record UpcomingMeetingDto(string Id, string Title, DateTime StartAt, DateTime EndAt, string? ContactName);

public record ForecastDto(decimal WeightedForecast30Days, decimal ConfidencePercentage, string Summary);
