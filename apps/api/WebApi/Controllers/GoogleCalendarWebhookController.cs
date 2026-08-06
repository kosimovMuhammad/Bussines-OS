using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("webhooks/google-calendar")]
[AllowAnonymous]
public class GoogleCalendarWebhookController(ICalendarConnectionService calendarConnectionService, IBackgroundTaskQueue queue) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var channelId = Request.Headers["X-Goog-Channel-Id"].ToString();
        var channelToken = Request.Headers["X-Goog-Channel-Token"].ToString();

        if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(channelToken) ||
            !await calendarConnectionService.VerifyChannelAsync(channelId, channelToken, ct))
        {
            return Unauthorized();
        }

        await queue.QueueAsync(async (sp, token) =>
        {
            var service = sp.GetRequiredService<ICalendarConnectionService>();
            await service.ProcessInboundChangesByChannelAsync(channelId, token);
        });

        return Ok(new { received = true });
    }
}
