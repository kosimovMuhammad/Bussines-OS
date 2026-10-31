using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
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
        var expected = configuration["INSTAGRAM_VERIFY_TOKEN"];
        if (mode == "subscribe" && !string.IsNullOrEmpty(expected) && verifyToken == expected)
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }

        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var signatureHeader = Request.Headers["X-Hub-Signature-256"].ToString();
        var appSecret = configuration["INSTAGRAM_APP_SECRET"];
        if (!VerifySignature(rawBody, signatureHeader, appSecret))
        {
            logger.LogWarning("Instagram webhook signature verification failed");
            return Unauthorized();
        }

        logger.LogInformation("Instagram webhook received, queuing for processing");

        await queue.QueueAsync(async (sp, token) =>
        {
            var service = sp.GetRequiredService<IInstagramService>();
            await service.ProcessWebhookAsync(rawBody, token);
        });

        return Ok(new { received = true });
    }

    private static bool VerifySignature(string body, string signatureHeader, string? appSecret)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith(prefix))
        {
            return false;
        }

        var providedHex = signatureHeader[prefix.Length..];

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var computedHex = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(providedHex));
    }
}
