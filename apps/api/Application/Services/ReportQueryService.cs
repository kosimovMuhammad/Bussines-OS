using System.Globalization;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Reports;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

// The security boundary for the whole generic Report engine lives here: EntityType,
// filter fields, GroupBy fields, and Metrics are all validated against a fixed per-entity
// whitelist, and every filter is applied via a hand-written, ordinary (non-dynamic) LINQ
// lambda per (entity, field) pair - deliberately not a generic Expression-tree combinator.
// A generic version built by wrapping compiled delegates back into an outer expression
// tree looks appealing but does NOT translate to SQL (EF Core sees an opaque delegate
// invoke, not a real member access, and throws at query time) - this verbose-but-explicit
// approach is the correct one, not a missed simplification.
public class ReportQueryService(IAppDbContext db) : IReportQueryService
{
    public static readonly IReadOnlySet<string> ReportableEntityTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Contacts", "Deals", "Tasks",
    };

    private static readonly IReadOnlySet<string> PeriodGroupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "day", "week", "month" };

    public async Task<ReportRunResultDto> RunAsync(
        string companyId, string entityType, IReadOnlyList<ReportFilterDto> filters,
        string? groupBy, IReadOnlyList<string> metrics, CancellationToken cancellationToken = default)
    {
        if (metrics.Count == 0)
        {
            throw new ValidationAppException(["At least one metric is required"]);
        }

        if (string.Equals(entityType, "Contacts", StringComparison.OrdinalIgnoreCase))
        {
            return await RunContactsAsync(companyId, filters, groupBy, metrics, cancellationToken);
        }

        if (string.Equals(entityType, "Deals", StringComparison.OrdinalIgnoreCase))
        {
            return await RunDealsAsync(companyId, filters, groupBy, metrics, cancellationToken);
        }

        if (string.Equals(entityType, "Tasks", StringComparison.OrdinalIgnoreCase))
        {
            return await RunTasksAsync(companyId, filters, groupBy, metrics, cancellationToken);
        }

        throw new ValidationAppException([$"Unknown entityType '{entityType}'. Valid values: {string.Join(", ", ReportableEntityTypes)}"]);
    }

    private record ContactRow(string? OwnerId, DateTime CreatedAt);
    private record DealRow(string Stage, string? OwnerId, decimal Value, DateTime CreatedAt);
    private record TaskRow(string Status, string Priority, string? AssignedTo, DateTime CreatedAt);

    private async Task<ReportRunResultDto> RunContactsAsync(string companyId, IReadOnlyList<ReportFilterDto> filters, string? groupBy, IReadOnlyList<string> metrics, CancellationToken cancellationToken)
    {
        var query = db.Contacts.Where(c => c.CompanyId == companyId);

        foreach (var f in filters)
        {
            var value = f.Value;
            var parsedDate = f.Field == "CreatedAt" ? ParseDate(f.Value) : default;

            query = (f.Field, f.Operator) switch
            {
                ("OwnerId", "eq") => query.Where(c => c.OwnerId == value),
                ("OwnerId", "neq") => query.Where(c => c.OwnerId != value),
                ("CreatedAt", "gt") => query.Where(c => c.CreatedAt > parsedDate),
                ("CreatedAt", "gte") => query.Where(c => c.CreatedAt >= parsedDate),
                ("CreatedAt", "lt") => query.Where(c => c.CreatedAt < parsedDate),
                ("CreatedAt", "lte") => query.Where(c => c.CreatedAt <= parsedDate),
                ("CreatedAt", "eq") => query.Where(c => c.CreatedAt == parsedDate),
                ("OwnerId", _) or ("CreatedAt", _) => throw UnknownOperator(f.Operator, f.Field),
                _ => throw UnknownFilterField(f.Field, "Contacts"),
            };
        }

        var rows = await query.Select(c => new ContactRow(c.OwnerId, c.CreatedAt)).ToListAsync(cancellationToken);

        return Aggregate(
            rows, groupBy, metrics,
            categorical: new Dictionary<string, Func<ContactRow, string?>> { ["OwnerId"] = r => r.OwnerId },
            dateSelector: r => r.CreatedAt,
            numeric: new Dictionary<string, Func<ContactRow, decimal>>(),
            entityTypeForErrors: "Contacts");
    }

    private async Task<ReportRunResultDto> RunDealsAsync(string companyId, IReadOnlyList<ReportFilterDto> filters, string? groupBy, IReadOnlyList<string> metrics, CancellationToken cancellationToken)
    {
        var query = db.Deals.Where(d => d.CompanyId == companyId);

        foreach (var f in filters)
        {
            var value = f.Value;
            var parsedDate = f.Field == "CreatedAt" ? ParseDate(f.Value) : default;
            var parsedDecimal = f.Field == "Value" ? ParseDecimal(f.Value) : default;
            var parsedStage = f.Field == "Stage" ? ParseEnum<DealStage>(f.Value) : default;

            query = (f.Field, f.Operator) switch
            {
                ("Stage", "eq") => query.Where(d => d.Stage == parsedStage),
                ("Stage", "neq") => query.Where(d => d.Stage != parsedStage),
                ("OwnerId", "eq") => query.Where(d => d.OwnerId == value),
                ("OwnerId", "neq") => query.Where(d => d.OwnerId != value),
                ("CreatedAt", "gt") => query.Where(d => d.CreatedAt > parsedDate),
                ("CreatedAt", "gte") => query.Where(d => d.CreatedAt >= parsedDate),
                ("CreatedAt", "lt") => query.Where(d => d.CreatedAt < parsedDate),
                ("CreatedAt", "lte") => query.Where(d => d.CreatedAt <= parsedDate),
                ("CreatedAt", "eq") => query.Where(d => d.CreatedAt == parsedDate),
                ("Value", "gt") => query.Where(d => d.Value > parsedDecimal),
                ("Value", "gte") => query.Where(d => d.Value >= parsedDecimal),
                ("Value", "lt") => query.Where(d => d.Value < parsedDecimal),
                ("Value", "lte") => query.Where(d => d.Value <= parsedDecimal),
                ("Value", "eq") => query.Where(d => d.Value == parsedDecimal),
                ("Stage", _) or ("OwnerId", _) or ("CreatedAt", _) or ("Value", _) => throw UnknownOperator(f.Operator, f.Field),
                _ => throw UnknownFilterField(f.Field, "Deals"),
            };
        }

        var rows = await query.Select(d => new DealRow(d.Stage.ToString(), d.OwnerId, d.Value, d.CreatedAt)).ToListAsync(cancellationToken);

        return Aggregate(
            rows, groupBy, metrics,
            categorical: new Dictionary<string, Func<DealRow, string?>> { ["Stage"] = r => r.Stage, ["OwnerId"] = r => r.OwnerId },
            dateSelector: r => r.CreatedAt,
            numeric: new Dictionary<string, Func<DealRow, decimal>> { ["value"] = r => r.Value },
            entityTypeForErrors: "Deals");
    }

    private async Task<ReportRunResultDto> RunTasksAsync(string companyId, IReadOnlyList<ReportFilterDto> filters, string? groupBy, IReadOnlyList<string> metrics, CancellationToken cancellationToken)
    {
        var query = db.Tasks.Where(t => t.CompanyId == companyId);

        foreach (var f in filters)
        {
            var value = f.Value;
            var parsedDate = f.Field == "CreatedAt" ? ParseDate(f.Value) : default;
            var parsedStatus = f.Field == "Status" ? ParseEnum<TaskItemStatus>(f.Value) : default;
            var parsedPriority = f.Field == "Priority" ? ParseEnum<TaskPriority>(f.Value) : default;

            query = (f.Field, f.Operator) switch
            {
                ("Status", "eq") => query.Where(t => t.Status == parsedStatus),
                ("Status", "neq") => query.Where(t => t.Status != parsedStatus),
                ("Priority", "eq") => query.Where(t => t.Priority == parsedPriority),
                ("Priority", "neq") => query.Where(t => t.Priority != parsedPriority),
                ("AssignedTo", "eq") => query.Where(t => t.AssignedTo == value),
                ("AssignedTo", "neq") => query.Where(t => t.AssignedTo != value),
                ("CreatedAt", "gt") => query.Where(t => t.CreatedAt > parsedDate),
                ("CreatedAt", "gte") => query.Where(t => t.CreatedAt >= parsedDate),
                ("CreatedAt", "lt") => query.Where(t => t.CreatedAt < parsedDate),
                ("CreatedAt", "lte") => query.Where(t => t.CreatedAt <= parsedDate),
                ("CreatedAt", "eq") => query.Where(t => t.CreatedAt == parsedDate),
                ("Status", _) or ("Priority", _) or ("AssignedTo", _) or ("CreatedAt", _) => throw UnknownOperator(f.Operator, f.Field),
                _ => throw UnknownFilterField(f.Field, "Tasks"),
            };
        }

        var rows = await query.Select(t => new TaskRow(t.Status.ToString(), t.Priority.ToString(), t.AssignedTo, t.CreatedAt)).ToListAsync(cancellationToken);

        return Aggregate(
            rows, groupBy, metrics,
            categorical: new Dictionary<string, Func<TaskRow, string?>> { ["Status"] = r => r.Status, ["Priority"] = r => r.Priority, ["AssignedTo"] = r => r.AssignedTo },
            dateSelector: r => r.CreatedAt,
            numeric: new Dictionary<string, Func<TaskRow, decimal>>(),
            entityTypeForErrors: "Tasks");
    }

    private static ReportRunResultDto Aggregate<T>(
        List<T> rows, string? groupBy, IReadOnlyList<string> metrics,
        IReadOnlyDictionary<string, Func<T, string?>> categorical,
        Func<T, DateTime> dateSelector,
        IReadOnlyDictionary<string, Func<T, decimal>> numeric,
        string entityTypeForErrors)
    {
        foreach (var metric in metrics)
        {
            if (metric == "count")
            {
                continue;
            }

            var parts = metric.Split(':', 2);
            if (parts.Length != 2 || (parts[0] != "sum" && parts[0] != "avg") || !numeric.ContainsKey(parts[1]))
            {
                throw new ValidationAppException([$"Unknown metric '{metric}' for entity '{entityTypeForErrors}'. Valid: count, sum:<field>, avg:<field> where field is one of: {string.Join(", ", numeric.Keys)}"]);
            }
        }

        Func<T, string?> keySelector;
        if (groupBy is null)
        {
            keySelector = _ => null;
        }
        else if (PeriodGroupKeys.Contains(groupBy))
        {
            var period = groupBy.ToLowerInvariant();
            keySelector = r => BucketDate(dateSelector(r), period);
        }
        else if (categorical.TryGetValue(groupBy, out var selector))
        {
            keySelector = r => selector(r) ?? "(none)";
        }
        else
        {
            throw new ValidationAppException([$"Unknown groupBy field '{groupBy}' for entity '{entityTypeForErrors}'. Valid: day, week, month, {string.Join(", ", categorical.Keys)}"]);
        }

        // Ungrouped aggregation over an empty filtered set should still report one row of
        // zeros ("0 records match") rather than no rows at all - GroupBy on an empty
        // sequence naturally yields zero groups, which would otherwise hide that distinction
        // from a genuinely-empty-because-of-bad-filters response.
        if (groupBy is null && rows.Count == 0)
        {
            var zeroMetrics = metrics.ToDictionary(m => m, _ => 0m);
            return new ReportRunResultDto([new ReportRowDto(null, zeroMetrics)]);
        }

        var groups = rows.GroupBy(keySelector).OrderBy(g => g.Key, StringComparer.Ordinal);

        var resultRows = groups.Select(g =>
        {
            var metricValues = new Dictionary<string, decimal>();
            foreach (var metric in metrics)
            {
                if (metric == "count")
                {
                    metricValues["count"] = g.Count();
                    continue;
                }

                var parts = metric.Split(':', 2);
                var field = numeric[parts[1]];
                metricValues[metric] = parts[0] == "sum" ? g.Sum(field) : (g.Any() ? g.Average(field) : 0m);
            }

            return new ReportRowDto(g.Key, metricValues);
        }).ToList();

        return new ReportRunResultDto(resultRows);
    }

    private static string BucketDate(DateTime date, string period) => period switch
    {
        "day" => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "month" => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        "week" => StartOfIsoWeek(date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static DateTime StartOfIsoWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private static DateTime ParseDate(string value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            throw new ValidationAppException([$"Invalid date value '{value}'"]);
        }

        return parsed;
    }

    private static decimal ParseDecimal(string value)
    {
        if (!decimal.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ValidationAppException([$"Invalid numeric value '{value}'"]);
        }

        return parsed;
    }

    private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            throw new ValidationAppException([$"Invalid value '{value}' for {typeof(TEnum).Name}. Valid values: {string.Join(", ", Enum.GetNames<TEnum>())}"]);
        }

        return parsed;
    }

    private static ValidationAppException UnknownFilterField(string field, string entityType) =>
        new([$"Unknown filter field '{field}' for entity '{entityType}'"]);

    private static ValidationAppException UnknownOperator(string op, string field) =>
        new([$"Unknown or unsupported operator '{op}' for field '{field}'"]);
}
