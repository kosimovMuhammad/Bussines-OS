namespace BusinessOS.Application.ViewModels.Reports;

public record EmployeePerformanceDto(
    string UserId,
    string UserName,
    int DealsWonCount,
    decimal DealsWonValue,
    int DealsLostCount,
    decimal DealsLostValue,
    int TasksCompleted,
    int TasksOverdue,
    double? AvgDealCycleDays,
    int ActivityCount);
