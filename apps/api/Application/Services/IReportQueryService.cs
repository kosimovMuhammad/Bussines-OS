using BusinessOS.Application.ViewModels.Reports;

namespace BusinessOS.Application.Services;

// The security boundary for the whole generic Report engine: EntityType, filter fields,
// GroupBy fields, and Metrics are all validated against a fixed per-entity whitelist here
// (ReportQueryService) - nothing from client input ever becomes a raw SQL fragment, table
// name, or arbitrary join. Unknown values throw ValidationAppException (422), never
// silently pass through.
public interface IReportQueryService
{
    Task<ReportRunResultDto> RunAsync(
        string companyId, string entityType, IReadOnlyList<ReportFilterDto> filters,
        string? groupBy, IReadOnlyList<string> metrics, CancellationToken cancellationToken = default);
}
