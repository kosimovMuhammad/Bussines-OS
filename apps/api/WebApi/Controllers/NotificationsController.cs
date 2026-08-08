using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Notifications;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
[RequireModule(ModuleName.Notifications)]
public class NotificationsController(
    INotificationService notificationService,
    ICurrentUserContext currentUser,
    IValidator<CreateNotificationRequest> createValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryNotificationsRequest query, CancellationToken ct) =>
        Ok(await notificationService.ListAsync(currentUser.UserId!, query, ct));

    [HttpGet("unread")]
    public async Task<IActionResult> ListUnread([FromQuery] QueryNotificationsRequest query, CancellationToken ct) =>
        Ok(await notificationService.ListAsync(currentUser.UserId!, query with { IsRead = false }, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Ok(new { count = await notificationService.GetUnreadCountAsync(currentUser.UserId!, ct) });

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await notificationService.GetAsync(currentUser.UserId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateNotificationRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await notificationService.CreateAsync(currentUser.CompanyId!, request, ct));
    }

    // RequireAction(Read) override on all three: these only ever touch the caller's own
    // inbox (service methods are hard-scoped to currentUser.UserId), so managing your own
    // notifications is self-service hygiene, not a privileged mutation - every role gets
    // Read by default. Same pattern as AuthController.LogoutAll and SessionsController.
    // POST (notifying someone ELSE) deliberately has no override - that's a real privileged
    // action, gated by the normal Create permission.
    [HttpPatch("{id}/read")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> MarkRead(string id, CancellationToken ct)
    {
        await notificationService.MarkReadAsync(currentUser.UserId!, id, ct);
        return NoContent();
    }

    [HttpPatch("read-all")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notificationService.MarkAllReadAsync(currentUser.UserId!, ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await notificationService.DeleteAsync(currentUser.UserId!, id, ct);
        return NoContent();
    }
}
