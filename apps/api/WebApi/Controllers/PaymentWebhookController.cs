using BusinessOS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

// Route carries the provider name for when multiple providers are supported later
// (currently unused - see IPaymentProvider's own placeholder-until-decided note). Always
// returns 200 (webhooks generally shouldn't be retried over unrecognized/duplicate
// events) with a body indicating whether anything was actually applied.
[ApiController]
[Route("webhooks/payments/{provider}")]
[AllowAnonymous]
public class PaymentWebhookController(IPaymentWebhookService webhookService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(string provider, CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        var applied = await webhookService.HandleAsync(rawBody, headers, ct);

        return Ok(new { applied });
    }
}
