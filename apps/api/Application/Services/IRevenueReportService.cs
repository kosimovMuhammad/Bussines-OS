using BusinessOS.Application.ViewModels.Reports;

namespace BusinessOS.Application.Services;

public interface IRevenueReportService
{
    Task<RevenueReportDto> GetAsync(string companyId, DateTime? from, DateTime? to, string groupBy, CancellationToken cancellationToken = default);
}
