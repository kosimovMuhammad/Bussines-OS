namespace BusinessOS.Application.ViewModels.Reports;

public record SalesByStageDto(string Stage, int Count, decimal Value);

public record SalesReportDto(
    int TotalDeals,
    int WonCount,
    decimal WonValue,
    int LostCount,
    decimal LostValue,
    int OpenCount,
    decimal OpenValue,
    double? WinRatePercent,
    decimal AverageDealValue,
    IReadOnlyList<SalesByStageDto> ByStage);

public record TasksByStatusDto(string Status, int Count);

public record TasksByPriorityDto(string Priority, int Count);

public record TasksReportDto(
    int TotalTasks,
    int CompletedCount,
    int OverdueCount,
    double? CompletionRatePercent,
    IReadOnlyList<TasksByStatusDto> ByStatus,
    IReadOnlyList<TasksByPriorityDto> ByPriority);

public record TopCustomerDto(string ContactId, string ContactName, decimal WonRevenue, int WonDealsCount);

public record CustomersReportDto(int TotalContacts, int NewContactsInRange, IReadOnlyList<TopCustomerDto> TopCustomers);
