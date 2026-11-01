using System.Text;
using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using BusinessOS.WebApi.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("webhooks/whatsapp")]
[AllowAnonymous]
public class WhatsAppWebhookController(IConfiguration configuration, IBackgroundTaskQueue queue, ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (WebhookSecurity.IsValidVerification(mode, verifyToken, configuration["WHATSAPP_VERIFY_TOKEN"]))
        {
            logger.LogInformation("WhatsApp webhook verification succeeded");
            return Content(challenge ?? string.Empty, "text/plain");
        }

        // The usual cause is a verify token in the Meta dashboard that differs from WHATSAPP_VERIFY_TOKEN.
        logger.LogWarning("WhatsApp webhook verification REJECTED (mode={Mode}); check the verify token in the Meta dashboard matches WHATSAPP_VERIFY_TOKEN", mode);
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
            // A wrong WHATSAPP_APP_SECRET (e.g. the Instagram app's instead of the main app's) lands here.
            logger.LogWarning("WhatsApp webhook REJECTED: invalid X-Hub-Signature-256 ({Bytes} bytes); check WHATSAPP_APP_SECRET is the main Meta app's secret", rawBytes.Length);
            return Unauthorized();
        }

        var json = Encoding.UTF8.GetString(rawBytes);
        logger.LogInformation("WhatsApp webhook received: {Bytes} bytes, fields=[{Fields}]", rawBytes.Length, DescribeFields(json));

        var messages = WhatsAppWebhookParser.ExtractIncomingMessages(json);
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

    // Field names only (messages, account_update, smb_message_echoes, ...) - never message content.
    private static string DescribeFields(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return "none";
            }

            var fields = entries.EnumerateArray()
                .Where(e => e.TryGetProperty("changes", out var c) && c.ValueKind == JsonValueKind.Array)
                .SelectMany(e => e.GetProperty("changes").EnumerateArray())
                .Select(c => c.TryGetProperty("field", out var f) ? f.GetString() : null)
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct();
            return string.Join(",", fields);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return "unparseable";
        }
    }
}
