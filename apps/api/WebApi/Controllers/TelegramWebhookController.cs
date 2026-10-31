using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("webhooks/telegram")]
[AllowAnonymous]
public class TelegramWebhookController(IBackgroundTaskQueue queue) : ControllerBase
{
    [HttpPost("{secret}")]
    public async Task<IActionResult> Receive(string secret, CancellationToken ct)
    {
        // Telegram echoes the secret_token we registered via setWebhook back on every call in
        // this header - it must match the {secret} segment in the URL we gave Telegram
        // (TelegramService.ConnectAccountAsync sets both to TelegramAccount.WebhookSecret).
        var headerToken = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        if (!VerifySecret(headerToken, secret))
        {
            return Unauthorized();
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        await queue.QueueAsync(async (sp, token) =>
        {
            var service = sp.GetRequiredService<ITelegramService>();
            await service.ProcessWebhookAsync(secret, rawBody, token);
        });

        return Ok(new { received = true });
    }

    private static bool VerifySecret(string headerToken, string pathSecret)
    {
        if (string.IsNullOrEmpty(headerToken) || string.IsNullOrEmpty(pathSecret))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(headerToken), Encoding.UTF8.GetBytes(pathSecret));
    }
}
