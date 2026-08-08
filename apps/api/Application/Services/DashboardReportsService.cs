using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Reports;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

// SQL-side aggregation throughout (GroupBy/Sum/Count translate to a single GROUP BY query
// each - Deals/Tasks/Contacts rows are never pulled into memory to be summed in C#), unlike
// RevenueReportService's by-period bucketing which materializes rows first because Postgres
// date-trunc-by-arbitrary-period doesn't translate cleanly through LINQ.
public class DashboardReportsService(IAppDbContext db) : IDashboardReportsService
{
    public async Task<SalesReportDto> GetSalesAsync(string companyId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var (effectiveFrom, effectiveTo) = ResolveRange(from, to);

        var byStage = await db.Deals
            .Where(d => d.CompanyId == companyId && d.CreatedAt >= effectiveFrom && d.CreatedAt <= effectiveTo)
            .GroupBy(d => d.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count(), Value = g.Sum(d => d.Value) })
            .ToListAsync(cancellationToken);

        var totalDeals = byStage.Sum(s => s.Count);
        var totalValue = byStage.Sum(s => s.Value);
        var won = byStage.FirstOrDefault(s => s.Stage == DealStage.Won);
        var lost = byStage.FirstOrDefault(s => s.Stage == DealStage.Lost);
        var wonCount = won?.Count ?? 0;
        var wonValue = won?.Value ?? 0m;
        var lostCount = lost?.Count ?? 0;
        var lostValue = lost?.Value ?? 0m;
        var closedCount = wonCount + lostCount;

        return new SalesReportDto(
            totalDeals, wonCount, wonValue, lostCount, lostValue,
            totalDeals - wonCount - lostCount, totalValue - wonValue - lostValue,
            closedCount > 0 ? (double)wonCount / closedCount * 100 : null,
            totalDeals > 0 ? totalValue / totalDeals : 0m,
            byStage.Select(s => new SalesByStageDto(s.Stage.ToString(), s.Count, s.Value)).ToList());
    }

    public async Task<TasksReportDto> GetTasksAsync(string companyId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var (effectiveFrom, effectiveTo) = ResolveRange(from, to);
        var now = DateTime.UtcNow;

        var byStatus = await db.Tasks
            .Where(t => t.CompanyId == companyId && t.CreatedAt >= effectiveFrom && t.CreatedAt <= effectiveTo)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byPriority = await db.Tasks
            .Where(t => t.CompanyId == companyId && t.CreatedAt >= effectiveFrom && t.CreatedAt <= effectiveTo)
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalTasks = byStatus.Sum(s => s.Count);
        var completedCount = byStatus.FirstOrDefault(s => s.Status == TaskItemStatus.Completed)?.Count ?? 0;

        var overdueCount = await db.Tasks.CountAsync(t =>
            t.CompanyId == companyId && t.CreatedAt >= effectiveFrom && t.CreatedAt <= effectiveTo
            && t.Status != TaskItemStatus.Completed && t.Status != TaskItemStatus.Archived
            && t.DueDate != null && t.DueDate < now, cancellationToken);

        return new TasksReportDto(
            totalTasks, completedCount, overdueCount,
            totalTasks > 0 ? (double)completedCount / totalTasks * 100 : null,
            byStatus.Select(s => new TasksByStatusDto(s.Status.ToString(), s.Count)).ToList(),
            byPriority.Select(p => new TasksByPriorityDto(p.Priority.ToString(), p.Count)).ToList());
    }

    public async Task<CustomersReportDto> GetCustomersAsync(string companyId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var (effectiveFrom, effectiveTo) = ResolveRange(from, to);

        var totalContacts = await db.Contacts.CountAsync(c => c.CompanyId == companyId, cancellationToken);
        var newContacts = await db.Contacts.CountAsync(
            c => c.CompanyId == companyId && c.CreatedAt >= effectiveFrom && c.CreatedAt <= effectiveTo, cancellationToken);

        var top = await db.Deals
            .Where(d => d.CompanyId == companyId && d.Stage == DealStage.Won && d.UpdatedAt >= effectiveFrom && d.UpdatedAt <= effectiveTo)
            .GroupBy(d => new { d.ContactId, d.Contact.FirstName, d.Contact.LastName })
            .Select(g => new { g.Key.ContactId, g.Key.FirstName, g.Key.LastName, Revenue = g.Sum(d => d.Value), Count = g.Count() })
            .OrderByDescending(x => x.Revenue)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new CustomersReportDto(totalContacts, newContacts,
            top.Select(t => new TopCustomerDto(t.ContactId, $"{t.FirstName} {t.LastName}".Trim(), t.Revenue, t.Count)).ToList());
    }

    // Same Kind=Unspecified -> UTC stamping convention as RevenueReportService/
    // EmployeePerformanceService; defaults to a trailing 30-day window when unset (Revenue
    // defaults to "start of to's month" instead, but these three reports aren't
    // calendar-month-shaped the way a revenue trendline is).
    private static (DateTime From, DateTime To) ResolveRange(DateTime? from, DateTime? to)
    {
        var effectiveTo = to is null ? DateTime.UtcNow : DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
        var effectiveFrom = from is null ? effectiveTo.AddDays(-30) : DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);

        if (effectiveFrom > effectiveTo)
        {
            throw new ValidationAppException(["'from' must be before or equal to 'to'"]);
        }

        return (effectiveFrom, effectiveTo);
    }
}
