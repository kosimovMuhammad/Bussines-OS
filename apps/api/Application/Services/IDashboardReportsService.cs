using BusinessOS.Application.ViewModels.Reports;

namespace BusinessOS.Application.Services;

public interface IDashboardReportsService
{
    Task<SalesReportDto> GetSalesAsync(string companyId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<TasksReportDto> GetTasksAsync(string companyId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<CustomersReportDto> GetCustomersAsync(string companyId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}
