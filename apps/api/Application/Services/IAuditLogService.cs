using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Audit;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Services;

public interface IAuditLogService
{
    // Stages an AuditLog row on the tracked DbContext - does NOT call SaveChangesAsync
    // itself. The caller's own SaveChangesAsync commits it atomically alongside the
    // actual mutation being audited (single transaction, no extra round trip).
    void Log(string companyId, string? userId, string action, string targetType, string? targetId = null, object? metadata = null, string? ipAddress = null, string? userAgent = null);

    Task<PagedResult<AuditLogDto>> ListAsync(string companyId, Role actorRole, QueryAuditLogRequest query, CancellationToken cancellationToken = default);

    Task<PagedResult<AuditLogDto>> GetSecurityHistoryAsync(string companyId, string userId, QueryAuditLogRequest query, CancellationToken cancellationToken = default);

    // Company-wide (not per-user) variants behind /audit/* - Owner/Admin only, same
    // defense-in-depth role check as ListAsync since the coarse [RequireModule(Audit)]
    // gate can be edited per role by an admin.
    Task<PagedResult<LoginHistoryDto>> GetCompanyLoginHistoryAsync(string companyId, Role actorRole, QueryLoginHistoryRequest query, CancellationToken cancellationToken = default);
    Task<PagedResult<AuditLogDto>> GetCompanySecurityHistoryAsync(string companyId, Role actorRole, QueryAuditLogRequest query, CancellationToken cancellationToken = default);
    Task<PagedResult<AuditLogDto>> GetPermissionHistoryAsync(string companyId, Role actorRole, QueryAuditLogRequest query, CancellationToken cancellationToken = default);
    Task<string> ExportCsvAsync(string companyId, Role actorRole, QueryAuditLogRequest query, CancellationToken cancellationToken = default);
}
