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
[Route("users/me/notification-preferences")]
[Authorize]
[RequireModule(ModuleName.Profile)]
public class NotificationPreferencesController(
    INotificationService notificationService,
    ICurrentUserContext currentUser,
    IValidator<UpdateNotificationPreferencesRequest> updatePreferencesValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await notificationService.GetPreferencesAsync(currentUser.UserId!, ct));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateNotificationPreferencesRequest request, CancellationToken ct)
    {
        await updatePreferencesValidator.ValidateAndThrowAppAsync(request, ct);
        await notificationService.UpdatePreferencesAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct);
        return NoContent();
    }
}
