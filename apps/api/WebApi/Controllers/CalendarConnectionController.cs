using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels;
using BusinessOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("calendar")]
[Authorize]
[RequireModule(ModuleName.Calendar)]
public class CalendarConnectionController(ICalendarConnectionService calendarConnectionService, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet("connect/google")]
    public IActionResult Connect() =>
        Ok(new OAuthUrlDto(calendarConnectionService.GetAuthUrl(currentUser.UserId!, currentUser.CompanyId!)));

    [HttpGet("connect/google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, CancellationToken ct)
    {
        await calendarConnectionService.HandleCallbackAsync(code, state, ct);
        return Ok(new { connected = true });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) =>
        Ok(await calendarConnectionService.GetStatusAsync(currentUser.CompanyId!, currentUser.UserId!, ct));

    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        await calendarConnectionService.DisconnectAsync(currentUser.CompanyId!, currentUser.UserId!, ct);
        return Ok(new { disconnected = true });
    }
}
