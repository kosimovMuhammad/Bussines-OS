using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("whatsapp")]
[Authorize]
[RequireModule(ModuleName.WhatsApp)]
public class WhatsAppController(
    IWhatsAppService whatsAppService,
    ICurrentUserContext currentUser,
    IValidator<ConnectWhatsAppAccountRequest> connectValidator,
    IValidator<SendWhatsAppRequest> sendValidator) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<IActionResult> ListAccounts(CancellationToken ct) =>
        Ok(await whatsAppService.ListAccountsAsync(currentUser.CompanyId!, ct));

    [HttpPost("accounts")]
    [Authorize ]
    public async Task<IActionResult> ConnectAccount(ConnectWhatsAppAccountRequest request, CancellationToken ct)
    {
        await connectValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await whatsAppService.ConnectAccountAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send(SendWhatsAppRequest request, CancellationToken ct)
    {
        await sendValidator.ValidateAndThrowAppAsync(request, ct);
        await whatsAppService.SendAsync(currentUser.CompanyId!, request, ct);
        return Ok(new { success = true });
    }
}
