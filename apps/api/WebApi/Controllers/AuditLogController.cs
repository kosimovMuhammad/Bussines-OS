using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Audit;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace BusinessOS.WebApi.Controllers;

// Read-only, system-generated, and sensitive - real Owner/Admin-only enforcement happens
// inside AuditLogService (see EnsureOwnerOrAdmin), on top of the coarse [RequireModule(Audit)]
// gate that an admin can otherwise edit per role.
[ApiController]
[Route("audit")]
[Authorize]
[RequireModule(ModuleName.Audit)]
public class AuditLogController(IAuditLogService auditLogService, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryAuditLogRequest query, CancellationToken ct) =>
        Ok(await auditLogService.ListAsync(currentUser.CompanyId!, currentUser.Role!.Value, query, ct));

    [HttpGet("login-history")]
    public async Task<IActionResult> LoginHistory([FromQuery] QueryLoginHistoryRequest query, CancellationToken ct) =>
        Ok(await auditLogService.GetCompanyLoginHistoryAsync(currentUser.CompanyId!, currentUser.Role!.Value, query, ct));

    [HttpGet("security")]
    public async Task<IActionResult> Security([FromQuery] QueryAuditLogRequest query, CancellationToken ct) =>
        Ok(await auditLogService.GetCompanySecurityHistoryAsync(currentUser.CompanyId!, currentUser.Role!.Value, query, ct));

    [HttpGet("permission-history")]
    public async Task<IActionResult> PermissionHistory([FromQuery] QueryAuditLogRequest query, CancellationToken ct) =>
        Ok(await auditLogService.GetPermissionHistoryAsync(currentUser.CompanyId!, currentUser.Role!.Value, query, ct));

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] QueryAuditLogRequest query, CancellationToken ct)
    {
        var csv = await auditLogService.ExportCsvAsync(currentUser.CompanyId!, currentUser.Role!.Value, query, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"audit-log-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}
