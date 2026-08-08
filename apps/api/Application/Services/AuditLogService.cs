using System.Text;
using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Audit;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class AuditLogService(IAppDbContext db) : IAuditLogService
{
    // Audit actions written by the Phase 1 RBAC endpoints (role/permission mutations) all
    // share this namespace, so /audit/permission-history can filter on a single prefix.
    // "user.role_changed" (written by UsersService, predates this namespace) is included
    // by name rather than renamed, to avoid disturbing an action string other code/tests
    // may already key off.
    public const string PermissionActionPrefix = "permission.";
    private const string LegacyRoleChangedAction = "user.role_changed";

    public void Log(string companyId, string? userId, string action, string targetType, string? targetId = null, object? metadata = null, string? ipAddress = null, string? userAgent = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = companyId,
            UserId = userId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
            IpAddress = ipAddress,
            UserAgent = userAgent,
        });
    }

    public async Task<PagedResult<AuditLogDto>> ListAsync(string companyId, Role actorRole, QueryAuditLogRequest query, CancellationToken cancellationToken = default)
    {
        // Coarse [RequireModule(Audit)] grants Read to Owner/Admin only by default (see the
        // AddSecurityAndVisibilityPhase migration seed), but that's editable per role - audit
        // log is governance data, so this hardcoded check is a defense-in-depth backstop.
        EnsureOwnerOrAdmin(actorRole);

        var logs = db.AuditLogs.Include(a => a.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logs = logs.Where(a => a.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            logs = logs.Where(a => a.UserId == query.UserId);
        }

        if (query.From is not null)
        {
            logs = logs.Where(a => a.CreatedAt >= query.From);
        }

        if (query.To is not null)
        {
            logs = logs.Where(a => a.CreatedAt <= query.To);
        }

        return await PageAsync(logs, query, cancellationToken);
    }

    public async Task<PagedResult<AuditLogDto>> GetSecurityHistoryAsync(string companyId, string userId, QueryAuditLogRequest query, CancellationToken cancellationToken = default)
    {
        var logs = db.AuditLogs.Include(a => a.User)
            .Where(a => a.UserId == userId && a.Action.StartsWith("security."));

        return await PageAsync(logs, query, cancellationToken);
    }

    public async Task<PagedResult<LoginHistoryDto>> GetCompanyLoginHistoryAsync(string companyId, Role actorRole, QueryLoginHistoryRequest query, CancellationToken cancellationToken = default)
    {
        EnsureOwnerOrAdmin(actorRole);

        var source = db.LoginHistories.Where(l => l.CompanyId == companyId);

        var total = await source.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await source
            .OrderByDescending(l => l.LoginAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<LoginHistoryDto>(
            items.Select(l => new LoginHistoryDto(l.Id, l.LoginAt, l.IpAddress, l.UserAgent, l.DeviceInfo, l.Success, l.FailureReason, l.IsNewDevice)).ToList(),
            page, limit, total);
    }

    public async Task<PagedResult<AuditLogDto>> GetCompanySecurityHistoryAsync(string companyId, Role actorRole, QueryAuditLogRequest query, CancellationToken cancellationToken = default)
    {
        EnsureOwnerOrAdmin(actorRole);

        var logs = db.AuditLogs.Include(a => a.User).Where(a => a.Action.StartsWith("security."));
        return await PageAsync(logs, query, cancellationToken);
    }

    public async Task<PagedResult<AuditLogDto>> GetPermissionHistoryAsync(string companyId, Role actorRole, QueryAuditLogRequest query, CancellationToken cancellationToken = default)
    {
        EnsureOwnerOrAdmin(actorRole);

        var logs = db.AuditLogs.Include(a => a.User)
            .Where(a => a.Action.StartsWith(PermissionActionPrefix) || a.Action == LegacyRoleChangedAction);

        return await PageAsync(logs, query, cancellationToken);
    }

    public async Task<string> ExportCsvAsync(string companyId, Role actorRole, QueryAuditLogRequest query, CancellationToken cancellationToken = default)
    {
        EnsureOwnerOrAdmin(actorRole);

        var logs = db.AuditLogs.Include(a => a.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logs = logs.Where(a => a.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            logs = logs.Where(a => a.UserId == query.UserId);
        }

        if (query.From is not null)
        {
            logs = logs.Where(a => a.CreatedAt >= query.From);
        }

        if (query.To is not null)
        {
            logs = logs.Where(a => a.CreatedAt <= query.To);
        }

        var items = await logs.OrderByDescending(a => a.CreatedAt).Take(10_000).ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Id,CreatedAt,UserId,UserName,Action,TargetType,TargetId,IpAddress");
        foreach (var a in items)
        {
            csv.AppendLine(string.Join(',',
                CsvField(a.Id), CsvField(a.CreatedAt.ToString("O")), CsvField(a.UserId),
                CsvField(a.User?.Name), CsvField(a.Action), CsvField(a.TargetType), CsvField(a.TargetId), CsvField(a.IpAddress)));
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

    private static void EnsureOwnerOrAdmin(Role actorRole)
    {
        if (actorRole is not (Role.Owner or Role.Admin))
        {
            throw new ForbiddenException("Only Owner/Admin can view the audit log");
        }
    }

    private static async Task<PagedResult<AuditLogDto>> PageAsync(IQueryable<AuditLog> source, QueryAuditLogRequest query, CancellationToken cancellationToken)
    {
        var total = await source.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await source
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    private static AuditLogDto ToDto(AuditLog a) => new(
        a.Id, a.UserId, a.User?.Name, a.Action, a.TargetType, a.TargetId, a.MetadataJson, a.IpAddress, a.CreatedAt);
}
