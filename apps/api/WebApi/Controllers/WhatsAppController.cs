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
    IValidator<HandleWhatsAppEmbeddedSignupRequest> embeddedSignupValidator,
    IValidator<UpdateWhatsAppAccountRequest> updateAccountValidator,
    IValidator<SendWhatsAppRequest> sendValidator,
    IValidator<SendPlatformWhatsAppTextRequest> sendPlatformTextValidator,
    IValidator<SendPlatformWhatsAppTemplateRequest> sendPlatformTemplateValidator) : ControllerBase
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

    [HttpPatch("accounts/{id}")]
    public async Task<IActionResult> UpdateAccount(string id, UpdateWhatsAppAccountRequest request, CancellationToken ct)
    {
        await updateAccountValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await whatsAppService.UpdateAccountAsync(currentUser.CompanyId!, currentUser.UserId!, id, request, ct));
    }

    [HttpDelete("accounts/{id}")]
    public async Task<IActionResult> RemoveAccount(string id, CancellationToken ct)
    {
        await whatsAppService.RemoveAccountAsync(currentUser.CompanyId!, currentUser.UserId!, id, ct);
        return NoContent();
    }

    // Called by the frontend right after Meta's WhatsApp Embedded Signup popup finishes - see
    // HandleWhatsAppEmbeddedSignupRequest for where Code/PhoneNumberId/WabaId come from.
    [HttpPost("embedded-signup")]
    public async Task<IActionResult> EmbeddedSignup(HandleWhatsAppEmbeddedSignupRequest request, CancellationToken ct)
    {
        await embeddedSignupValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await whatsAppService.HandleEmbeddedSignupAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send(SendWhatsAppRequest request, CancellationToken ct)
    {
        await sendValidator.ValidateAndThrowAppAsync(request, ct);
        await whatsAppService.SendAsync(currentUser.CompanyId!, request, ct);
        return Ok(new { success = true });
    }

    // Platform mode sends from the platform's own WhatsApp number (not a tenant's connected
    // account). The [RequireAction(Read)] override makes the coarse RBAC gate a passthrough -
    // same pattern as PlansController's platform-admin-only mutations - the real check is
    // WhatsAppService.EnsurePlatformAdminAsync.
    [HttpPost("platform/send/text")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> SendPlatformText(SendPlatformWhatsAppTextRequest request, CancellationToken ct)
    {
        await sendPlatformTextValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await whatsAppService.SendPlatformTextMessageAsync(currentUser.UserId!, request, ct));
    }

    [HttpPost("platform/send/template")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> SendPlatformTemplate(SendPlatformWhatsAppTemplateRequest request, CancellationToken ct)
    {
        await sendPlatformTemplateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await whatsAppService.SendPlatformTemplateMessageAsync(currentUser.UserId!, request, ct));
    }
}
