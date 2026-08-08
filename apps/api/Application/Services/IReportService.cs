using BusinessOS.Application.ViewModels.Reports;

namespace BusinessOS.Application.Services;

public interface IReportService
{
    Task<IReadOnlyList<ReportDto>> ListAsync(string companyId, CancellationToken cancellationToken = default);

    Task<ReportDto> CreateAsync(string companyId, string? createdBy, CreateReportRequest request, CancellationToken cancellationToken = default);

    Task<ReportRunResultDto> RunAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task DeleteAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
