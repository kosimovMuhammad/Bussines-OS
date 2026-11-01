using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using BusinessOS.WebApi.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusinessOS.WebApi.Controllers;

// Single shared endpoint for every tenant (Instagram webhook subscriptions are app-level, not
// per-account) - same verification pattern as WhatsAppWebhookController since both ride on
// Meta's Graph API webhook framework.
[ApiController]
[Route("webhooks/instagram")]
[AllowAnonymous]
public class InstagramWebhookController(
    IConfiguration configuration,
    IBackgroundTaskQueue queue,
    ILogger<InstagramWebhookController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (WebhookSecurity.IsValidVerification(mode, verifyToken, configuration["INSTAGRAM_VERIFY_TOKEN"]))
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
        if (!WebhookSecurity.IsValidMetaSignature(rawBytes, signatureHeader, configuration["INSTAGRAM_APP_SECRET"]))
        {
            logger.LogWarning("Instagram webhook signature verification failed");
            return Unauthorized();
        }

        var rawBody = Encoding.UTF8.GetString(rawBytes);
        logger.LogInformation("Instagram webhook received, queuing for processing");

        await queue.QueueAsync(async (sp, token) =>
        {
            var service = sp.GetRequiredService<IInstagramService>();
            await service.ProcessWebhookAsync(rawBody, token);
        });

        return Ok(new { received = true });
    }
}
