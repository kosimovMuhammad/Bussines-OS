using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Activity;
using BusinessOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

// Read-only and system-generated: rows are written exclusively by
// ActivityLogSaveChangesInterceptor whenever an IActivityLoggable entity (Contact, Deal,
// TaskItem) is created/updated/deleted - there is deliberately no POST here.
[ApiController]
[Route("activity")]
[Authorize]
[RequireModule(ModuleName.Activity)]
public class ActivityLogController(IActivityLogService activityLogService, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryActivityLogRequest query, CancellationToken ct) =>
        Ok(await activityLogService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetForUser(string userId, [FromQuery] QueryActivityLogRequest query, CancellationToken ct) =>
        Ok(await activityLogService.GetForUserAsync(currentUser.CompanyId!, userId, query, ct));

    [HttpGet("contact/{contactId}")]
    public async Task<IActionResult> GetForContact(string contactId, [FromQuery] QueryActivityLogRequest query, CancellationToken ct) =>
        Ok(await activityLogService.GetForEntityAsync(currentUser.CompanyId!, "Contact", contactId, query, ct));

    [HttpGet("deal/{dealId}")]
    public async Task<IActionResult> GetForDeal(string dealId, [FromQuery] QueryActivityLogRequest query, CancellationToken ct) =>
        Ok(await activityLogService.GetForEntityAsync(currentUser.CompanyId!, "Deal", dealId, query, ct));

    [HttpGet("task/{taskId}")]
    public async Task<IActionResult> GetForTask(string taskId, [FromQuery] QueryActivityLogRequest query, CancellationToken ct) =>
        Ok(await activityLogService.GetForEntityAsync(currentUser.CompanyId!, "TaskItem", taskId, query, ct));
}
