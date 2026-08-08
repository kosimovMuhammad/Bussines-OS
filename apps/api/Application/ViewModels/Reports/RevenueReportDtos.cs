namespace BusinessOS.Application.ViewModels.Reports;

public record RevenuePeriodBucket(string PeriodLabel, decimal Revenue);

public record RevenueByStageDto(string Stage, decimal Value, int Count);

public record RevenueReportDto(
    decimal TotalRevenue,
    decimal ForecastedRevenue,
    IReadOnlyList<RevenuePeriodBucket> ByPeriod,
    IReadOnlyList<RevenueByStageDto> ByStage,
    decimal PreviousPeriodRevenue,
    double? PercentChange);
