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
    IWhatsAppOnboardingService onboardingService,
    IValidator<CompleteWhatsAppEmbeddedSignupRequest> completeValidator,
    IValidator<WhatsAppSignupClientEventRequest> clientEventValidator,
    IValidator<SendWhatsAppRequest> sendValidator,
    IValidator<SendPlatformWhatsAppTextRequest> sendPlatformTextValidator,
    IValidator<SendPlatformWhatsAppTemplateRequest> sendPlatformTemplateValidator) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<IActionResult> ListAccounts(CancellationToken ct) =>
        Ok(await whatsAppService.ListAccountsAsync(currentUser.CompanyId!, ct));

    // There is deliberately no POST/PATCH on /accounts: accounts are created only by Meta
    // Embedded Signup (below) - users never type a phone_number_id, WABA id or token.
    [HttpDelete("accounts/{id}")]
    public async Task<IActionResult> RemoveAccount(string id, CancellationToken ct)
    {
        await whatsAppService.RemoveAccountAsync(currentUser.CompanyId!, currentUser.UserId!, id, ct);
        return NoContent();
    }

    // ---- Meta Embedded Signup (the only way to connect a number) -------------------------------

    // Everything FB.login needs + a signed state. `locale` (en/ru/tj) rides in the state.
    [HttpGet("embedded-signup/config")]
    public IActionResult EmbeddedSignupConfig([FromQuery] string? locale) =>
        Ok(onboardingService.GetConfig(currentUser.UserId!, currentUser.CompanyId!, locale));

    // Called right after the popup returns a code. Body is camelCase JSON: { code, state, session }
    // where session mirrors the WA_EMBEDDED_SIGNUP message event (all fields are hints only).
    [HttpPost("embedded-signup/complete")]
    public async Task<IActionResult> CompleteEmbeddedSignup(CompleteWhatsAppEmbeddedSignupRequest request, CancellationToken ct)
    {
        await completeValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await onboardingService.CompleteAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    // Popup CANCEL / ERROR events, so failures and Meta's session id are logged server-side; the
    // response is the stable error code + English fallback text for the UI.
    [HttpPost("embedded-signup/client-event")]
    public async Task<IActionResult> ReportEmbeddedSignupClientEvent(WhatsAppSignupClientEventRequest request, CancellationToken ct)
    {
        await clientEventValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(onboardingService.ReportClientEvent(currentUser.CompanyId!, currentUser.UserId!, request));
    }

    // Re-runs register / webhook subscription for an account left in NeedsAttention.
    [HttpPost("accounts/{id}/retry")]
    public async Task<IActionResult> RetryAccount(string id, CancellationToken ct) =>
        Ok(await onboardingService.RetryAsync(currentUser.CompanyId!, currentUser.UserId!, id, ct));

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
