namespace BusinessOS.Application.ViewModels.Reports;

public record ReportFilterDto(string Field, string Operator, string Value);

public record CreateReportRequest(string Name, string EntityType, IReadOnlyList<ReportFilterDto>? Filters, string? GroupBy, IReadOnlyList<string> Metrics);

public record ReportDto(
    string Id, string Name, string EntityType, IReadOnlyList<ReportFilterDto> Filters,
    string? GroupBy, IReadOnlyList<string> Metrics, string? CreatedBy, DateTime CreatedAt);

public record ReportRowDto(string? GroupKey, IReadOnlyDictionary<string, decimal> Metrics);

public record ReportRunResultDto(IReadOnlyList<ReportRowDto> Rows);
