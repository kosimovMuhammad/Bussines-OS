using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Email;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("email")]
[Authorize]
[RequireModule(ModuleName.Email)]
public class EmailController(
    IEmailService emailService,
    ICurrentUserContext currentUser,
    IValidator<SendEmailRequest> sendValidator) : ControllerBase
{
    [HttpGet("connect/gmail")]
    public IActionResult ConnectGmail() =>
        Ok(new OAuthUrlDto(emailService.GetGmailAuthUrl(currentUser.UserId!, currentUser.CompanyId!)));

    [HttpGet("connect/gmail/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> GmailCallback([FromQuery] string code, [FromQuery] string state, CancellationToken ct)
    {
        await emailService.HandleGmailCallbackAsync(code, state, ct);
        return Ok(new { connected = true });
    }

    [HttpGet("connect/outlook")]
    public IActionResult ConnectOutlook() =>
        Ok(new OAuthUrlDto(emailService.GetOutlookAuthUrl(currentUser.UserId!, currentUser.CompanyId!)));

    [HttpGet("connect/outlook/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OutlookCallback([FromQuery] string code, [FromQuery] string state, CancellationToken ct)
    {
        await emailService.HandleOutlookCallbackAsync(code, state, ct);
        return Ok(new { connected = true });
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send(SendEmailRequest request, CancellationToken ct)
    {
        await sendValidator.ValidateAndThrowAppAsync(request, ct);
        await emailService.SendAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct);
        return Ok(new { success = true });
    }
}
