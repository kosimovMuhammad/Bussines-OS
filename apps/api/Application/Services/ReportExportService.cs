using System.Text;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

// CSV shape/escaping matches AuditLogService.ExportCsvAsync's convention exactly (that's
// the existing /audit/export precedent this reuses) - this orchestrates across the report
// services rather than duplicating their aggregation logic, since AuditLogService only ever
// exports its own one table.
public class ReportExportService(
    IRevenueReportService revenueReportService,
    IDashboardReportsService dashboardReportsService,
    IEmployeePerformanceService employeePerformanceService) : IReportExportService
{
    private static readonly IReadOnlySet<string> ValidTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "revenue", "sales", "tasks", "customers", "employees",
    };

    public async Task<string> ExportCsvAsync(
        string companyId,
        string type,
        string requestingUserId,
        Role requestingUserRole,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        if (!ValidTypes.Contains(type))
        {
            throw new ValidationAppException([$"Unknown report type '{type}'. Valid types: {string.Join(", ", ValidTypes)}"]);
        }

        var csv = new StringBuilder();

        switch (type.ToLowerInvariant())
        {
            case "revenue":
                var revenue = await revenueReportService.GetAsync(companyId, from, to, "month", cancellationToken);
                csv.AppendLine("PeriodLabel,Revenue");
                foreach (var bucket in revenue.ByPeriod)
                {
                    csv.AppendLine(string.Join(',', CsvField(bucket.PeriodLabel), CsvField(bucket.Revenue.ToString())));
                }

                break;

            case "sales":
                var sales = await dashboardReportsService.GetSalesAsync(companyId, from, to, cancellationToken);
                csv.AppendLine("Stage,Count,Value");
                foreach (var stage in sales.ByStage)
                {
                    csv.AppendLine(string.Join(',', CsvField(stage.Stage), CsvField(stage.Count.ToString()), CsvField(stage.Value.ToString())));
                }

                break;

            case "tasks":
                var tasks = await dashboardReportsService.GetTasksAsync(companyId, from, to, cancellationToken);
                csv.AppendLine("Status,Count");
                foreach (var status in tasks.ByStatus)
                {
                    csv.AppendLine(string.Join(',', CsvField(status.Status), CsvField(status.Count.ToString())));
                }

                break;

            case "customers":
                var customers = await dashboardReportsService.GetCustomersAsync(companyId, from, to, cancellationToken);
                csv.AppendLine("ContactId,ContactName,WonRevenue,WonDealsCount");
                foreach (var customer in customers.TopCustomers)
                {
                    csv.AppendLine(string.Join(',',
                        CsvField(customer.ContactId), CsvField(customer.ContactName), CsvField(customer.WonRevenue.ToString()), CsvField(customer.WonDealsCount.ToString())));
                }

                break;

            case "employees":
                var employees = await employeePerformanceService.GetAsync(companyId, requestingUserId, requestingUserRole, null, from, to, cancellationToken);
                csv.AppendLine("UserId,UserName,DealsWonCount,DealsWonValue,DealsLostCount,DealsLostValue,TasksCompleted,TasksOverdue,AvgDealCycleDays");
                foreach (var employee in employees)
                {
                    csv.AppendLine(string.Join(',',
                        CsvField(employee.UserId), CsvField(employee.UserName), CsvField(employee.DealsWonCount.ToString()),
                        CsvField(employee.DealsWonValue.ToString()), CsvField(employee.DealsLostCount.ToString()), CsvField(employee.DealsLostValue.ToString()),
                        CsvField(employee.TasksCompleted.ToString()), CsvField(employee.TasksOverdue.ToString()), CsvField(employee.AvgDealCycleDays?.ToString("F1"))));
                }

                break;
        }

        return csv.ToString();
    }

    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
