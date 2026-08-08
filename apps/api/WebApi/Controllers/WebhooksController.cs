using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Integrations;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

// Outbound webhook management (this company's system notifying external URLs on its own
// events). Distinct from /webhooks/whatsapp, /webhooks/google-calendar, /webhooks/payments
// (see those controllers) - those are inbound receivers with [AllowAnonymous] + their own
// provider-specific signature verification; this is authenticated, tenant-scoped CRUD.
[ApiController]
[Route("webhooks")]
[Authorize]
[RequireModule(ModuleName.Webhooks)]
public class WebhooksController(
    IWebhooksService webhooksService,
    ICurrentUserContext currentUser,
    IValidator<CreateWebhookRequest> createValidator,
    IValidator<UpdateWebhookRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await webhooksService.ListAsync(currentUser.CompanyId!, ct));

    [HttpGet("logs")]
    public async Task<IActionResult> Logs([FromQuery] QueryWebhookLogsRequest query, CancellationToken ct) =>
        Ok(await webhooksService.ListLogsAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await webhooksService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateWebhookRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await webhooksService.CreateAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateWebhookRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await webhooksService.UpdateAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await webhooksService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }
}
