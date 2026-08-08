using System.Globalization;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Reports;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

// "Closed date" for Won/Lost deals is approximated as UpdatedAt - Deal has no dedicated
// ClosedAt field. This is the same documented assumption used in
// EmployeePerformanceService's cycle-time calculation.
public class RevenueReportService(IAppDbContext db) : IRevenueReportService
{
    private static readonly HashSet<string> ValidGroupBy = new(StringComparer.OrdinalIgnoreCase) { "day", "week", "month", "quarter" };

    public async Task<RevenueReportDto> GetAsync(string companyId, DateTime? from, DateTime? to, string groupBy, CancellationToken cancellationToken = default)
    {
        if (!ValidGroupBy.Contains(groupBy))
        {
            throw new ValidationAppException([$"Unknown groupBy '{groupBy}'. Valid values: {string.Join(", ", ValidGroupBy)}"]);
        }

        // Model-bound query-string DateTimes come back Kind=Unspecified - Npgsql refuses
        // to write those to a "timestamp with time zone" column ("only UTC is supported").
        // The API's convention is that unqualified dates mean UTC, so stamp that in rather
        // than silently reinterpreting via local server time.
        var effectiveTo = AsUtc(to) ?? DateTime.UtcNow;
        var effectiveFrom = AsUtc(from) ?? new DateTime(effectiveTo.Year, effectiveTo.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (effectiveFrom > effectiveTo)
        {
            throw new ValidationAppException(["'from' must be before or equal to 'to'"]);
        }

        var wonInPeriod = await db.Deals
            .Where(d => d.CompanyId == companyId && d.Stage == DealStage.Won && d.UpdatedAt >= effectiveFrom && d.UpdatedAt <= effectiveTo)
            .Select(d => new { d.Value, d.UpdatedAt })
            .ToListAsync(cancellationToken);

        var totalRevenue = wonInPeriod.Sum(d => d.Value);

        var byPeriod = wonInPeriod
            .GroupBy(d => BucketDate(d.UpdatedAt, groupBy.ToLowerInvariant()))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new RevenuePeriodBucket(g.Key, g.Sum(d => d.Value)))
            .ToList();

        // Distribution across pipeline stages for deals created in the same window -
        // reflects what entered the pipeline in this period, not just what closed.
        var byStage = await db.Deals
            .Where(d => d.CompanyId == companyId && d.CreatedAt >= effectiveFrom && d.CreatedAt <= effectiveTo)
            .GroupBy(d => d.Stage)
            .Select(g => new { Stage = g.Key, Value = g.Sum(d => d.Value), Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Forecast is a current snapshot of the open pipeline, independent of the
        // reporting window - "what's still open right now", not bounded by [from,to].
        var openDeals = await db.Deals
            .Where(d => d.CompanyId == companyId && d.Stage != DealStage.Won && d.Stage != DealStage.Lost)
            .Select(d => new { d.Value, d.Probability })
            .ToListAsync(cancellationToken);
        var forecastedRevenue = openDeals.Sum(d => d.Value * d.Probability / 100m);

        var periodLength = effectiveTo - effectiveFrom;
        var previousFrom = effectiveFrom - periodLength;
        var previousTo = effectiveFrom;
        var previousRevenue = await db.Deals
            .Where(d => d.CompanyId == companyId && d.Stage == DealStage.Won && d.UpdatedAt >= previousFrom && d.UpdatedAt < previousTo)
            .SumAsync(d => d.Value, cancellationToken);

        double? percentChange = previousRevenue == 0
            ? null
            : (double)((totalRevenue - previousRevenue) / previousRevenue * 100m);

        return new RevenueReportDto(
            totalRevenue, forecastedRevenue, byPeriod,
            byStage.Select(s => new RevenueByStageDto(s.Stage.ToString(), s.Value, s.Count)).ToList(),
            previousRevenue, percentChange);
    }

    private static string BucketDate(DateTime date, string period) => period switch
    {
        "day" => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "month" => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        "week" => StartOfIsoWeek(date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "quarter" => $"{date.Year}-Q{(date.Month - 1) / 3 + 1}",
        _ => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static DateTime StartOfIsoWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private static DateTime? AsUtc(DateTime? value) =>
        value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}
