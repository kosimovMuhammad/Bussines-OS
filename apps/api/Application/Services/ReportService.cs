using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Reports;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class ReportService(IAppDbContext db, IReportQueryService queryService) : IReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<ReportDto>> ListAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var reports = await db.Reports
            .Where(r => r.CompanyId == companyId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return reports.Select(ToDto).ToList();
    }

    public async Task<ReportDto> CreateAsync(string companyId, string? createdBy, CreateReportRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationAppException(["Name is required"]);
        }

        if (!ReportQueryService.ReportableEntityTypes.Contains(request.EntityType))
        {
            throw new ValidationAppException([$"Unknown entityType '{request.EntityType}'. Valid values: {string.Join(", ", ReportQueryService.ReportableEntityTypes)}"]);
        }

        if (request.Metrics.Count == 0)
        {
            throw new ValidationAppException(["At least one metric is required"]);
        }

        // Validate the config actually runs (whitelist enforcement, bad field/operator/value
        // combinations) before persisting it - a saved report that always 422s on /run is a
        // config the user should be told about immediately, not on their next visit.
        await queryService.RunAsync(companyId, request.EntityType, request.Filters ?? [], request.GroupBy, request.Metrics, cancellationToken);

        var report = new Report
        {
            CompanyId = companyId,
            Name = request.Name.Trim(),
            EntityType = request.EntityType,
            FiltersJson = JsonSerializer.Serialize(request.Filters ?? []),
            GroupBy = request.GroupBy,
            MetricsJson = JsonSerializer.Serialize(request.Metrics),
            CreatedBy = createdBy,
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(report);
    }

    public async Task<ReportRunResultDto> RunAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var report = await db.Reports.SingleOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Report not found");

        var filters = DeserializeFilters(report.FiltersJson);
        var metrics = JsonSerializer.Deserialize<List<string>>(report.MetricsJson, JsonOptions) ?? [];

        return await queryService.RunAsync(companyId, report.EntityType, filters, report.GroupBy, metrics, cancellationToken);
    }

    public async Task DeleteAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var report = await db.Reports.SingleOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Report not found");

        db.Reports.Remove(report);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<ReportFilterDto> DeserializeFilters(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<ReportFilterDto>>(json, JsonOptions) ?? [];

    private static ReportDto ToDto(Report r) => new(
        r.Id, r.Name, r.EntityType, DeserializeFilters(r.FiltersJson), r.GroupBy,
        JsonSerializer.Deserialize<List<string>>(r.MetricsJson, JsonOptions) ?? [], r.CreatedBy, r.CreatedAt);
}
