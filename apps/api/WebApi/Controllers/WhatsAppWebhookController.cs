using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using BusinessOS.WebApi.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("webhooks/whatsapp")]
[AllowAnonymous]
public class WhatsAppWebhookController(IConfiguration configuration, IBackgroundTaskQueue queue) : ControllerBase
{
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (WebhookSecurity.IsValidVerification(mode, verifyToken, configuration["WHATSAPP_VERIFY_TOKEN"]))
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }

        return Forbid();
    }

    [HttpPost]
    [RequestSizeLimit(WebhookSecurity.MaxWebhookBodyBytes)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var rawBytes = await WebhookSecurity.ReadBodyAsync(Request, ct);

        var signatureHeader = Request.Headers["X-Hub-Signature-256"].ToString();
        if (!WebhookSecurity.IsValidMetaSignature(rawBytes, signatureHeader, configuration["WHATSAPP_APP_SECRET"]))
        {
            return Unauthorized();
        }

        var messages = WhatsAppWebhookParser.ExtractIncomingMessages(Encoding.UTF8.GetString(rawBytes));
        foreach (var message in messages)
        {
            await queue.QueueAsync(async (sp, token) =>
            {
                var service = sp.GetRequiredService<IWhatsAppService>();
                await service.ProcessIncomingMessageAsync(message, token);
            });
        }

        return Ok(new { received = true });
    }
}
