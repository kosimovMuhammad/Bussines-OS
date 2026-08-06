using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Calendar;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("meetings")]
[Authorize]
[RequireModule(ModuleName.Calendar)]
public class CalendarController(
    ICalendarService calendarService,
    ICurrentUserContext currentUser,
    IValidator<CreateMeetingRequest> createValidator,
    IValidator<UpdateMeetingRequest> updateValidator,
    IValidator<UpdateMeetingStatusRequest> updateStatusValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryMeetingsRequest query, CancellationToken ct) =>
        Ok(await calendarService.ListAsync(currentUser.CompanyId!, currentUser.UserId!, currentUser.Role!.Value, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await calendarService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateMeetingRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await calendarService.CreateAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateMeetingRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await calendarService.UpdateAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, UpdateMeetingStatusRequest request, CancellationToken ct)
    {
        await updateStatusValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await calendarService.UpdateStatusAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await calendarService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }
}
