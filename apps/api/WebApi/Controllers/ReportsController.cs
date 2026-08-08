using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Reports;
using BusinessOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

// Reports Phase 5 note: no dedicated "Reports" RBAC module was added, deliberately - these
// are read-only aggregates over data every role already has some visibility into elsewhere
// (Deals/Tasks/Contacts/Users), with no PII beyond that. Analytics is already this
// codebase's "reads broadly across the company, granted to every role by default" module
// (same mapping Search's aggregate /search endpoint uses) - reusing it here keeps that one
// broad-read concept in one place instead of fragmenting it across a second module that
// would end up with an identical default grant anyway.
[ApiController]
[Route("reports")]
[Authorize]
[RequireModule(ModuleName.Analytics)]
public class ReportsController(
    IReportService reportService,
    IEmployeePerformanceService employeePerformanceService,
    IRevenueReportService revenueReportService,
    IDashboardReportsService dashboardReportsService,
    IReportExportService reportExportService,
    ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await reportService.ListAsync(currentUser.CompanyId!, ct));

    [HttpPost]
    [RequireAction(PermissionAction.Create)]
    public async Task<IActionResult> Create(CreateReportRequest request, CancellationToken ct) =>
        Ok(await reportService.CreateAsync(currentUser.CompanyId!, currentUser.UserId, request, ct));

    [HttpGet("{id}/run")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Run(string id, CancellationToken ct) =>
        Ok(await reportService.RunAsync(currentUser.CompanyId!, id, ct));

    [HttpDelete("{id}")]
    [RequireAction(PermissionAction.Delete)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await reportService.DeleteAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpGet("employee-performance")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> EmployeePerformance([FromQuery] string? userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (currentUser.Role is null)
        {
            throw new ForbiddenException("No role claim on the current user");
        }

        var result = await employeePerformanceService.GetAsync(currentUser.CompanyId!, currentUser.UserId!, currentUser.Role.Value, userId, from, to, ct);
        return Ok(result);
    }

    [HttpGet("revenue")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Revenue([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string groupBy = "month", CancellationToken ct = default) =>
        Ok(await revenueReportService.GetAsync(currentUser.CompanyId!, from, to, groupBy, ct));

    [HttpGet("sales")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Sales([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await dashboardReportsService.GetSalesAsync(currentUser.CompanyId!, from, to, ct));

    [HttpGet("tasks")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Tasks([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await dashboardReportsService.GetTasksAsync(currentUser.CompanyId!, from, to, ct));

    [HttpGet("customers")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Customers([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await dashboardReportsService.GetCustomersAsync(currentUser.CompanyId!, from, to, ct));

    // Literal /reports/employees route the Phase 5 spec asks for - thin delegation to the
    // same IEmployeePerformanceService /reports/employee-performance already uses (kept
    // both routes; nothing in this codebase depended on removing the older one).
    [HttpGet("employees")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Employees([FromQuery] string? userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (currentUser.Role is null)
        {
            throw new ForbiddenException("No role claim on the current user");
        }

        return Ok(await employeePerformanceService.GetAsync(currentUser.CompanyId!, currentUser.UserId!, currentUser.Role.Value, userId, from, to, ct));
    }

    [HttpGet("export")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Export([FromQuery] string type, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (currentUser.Role is null)
        {
            throw new ForbiddenException("No role claim on the current user");
        }

        var csv = await reportExportService.ExportCsvAsync(currentUser.CompanyId!, type, currentUser.UserId!, currentUser.Role.Value, from, to, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"report-{type}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}
