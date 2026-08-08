using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Webhooks;

// Best-effort delivery, per the Phase 6 spec: one attempt, short timeout, no retry queue -
// same "acceptable durability tradeoff" reasoning IBackgroundTaskQueue's own doc comment
// already applies to this codebase's background work in general. Every attempt (success or
// failure) is logged to WebhookDeliveryLog regardless.
public class WebhookDeliveryService(IAppDbContext db, ITokenCipher tokenCipher, IHttpClientFactory httpClientFactory) : IWebhookDeliveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task HandleEntityChangedAsync(EntityChangedEvent evt, CancellationToken cancellationToken = default)
    {
        // "deal.created", "contact.updated", etc. - lower-cased EntityType + Kind, no
        // friendly-name mapping table (e.g. TaskItem produces "taskitem.created", not
        // "task.created") to avoid a second naming scheme that needs to stay in sync with
        // entity class names.
        var eventName = $"{evt.EntityType.ToLowerInvariant()}.{evt.Kind.ToString().ToLowerInvariant()}";

        var webhooks = await db.Webhooks
            .Where(w => w.CompanyId == evt.CompanyId && w.IsActive && w.SubscribedEvents.Contains(eventName))
            .ToListAsync(cancellationToken);

        foreach (var webhook in webhooks)
        {
            await DeliverAsync(webhook, eventName, evt, cancellationToken);
        }
    }

    private async Task DeliverAsync(Webhook webhook, string eventName, EntityChangedEvent evt, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            Event = eventName,
            EntityType = evt.EntityType,
            EntityId = evt.EntityId,
            Timestamp = DateTime.UtcNow,
        }, JsonOptions);

        var log = new WebhookDeliveryLog
        {
            CompanyId = webhook.CompanyId,
            WebhookId = webhook.Id,
            Event = eventName,
            Url = webhook.Url,
        };

        try
        {
            var secret = tokenCipher.Decrypt(webhook.SecretEncrypted);
            var signature = ComputeSignature(payload, secret);

            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            // Same "sha256=<hex hmac>" shape WhatsAppWebhookController verifies on the
            // inbound side (X-Hub-Signature-256) - mirrored here for the send direction.
            request.Headers.Add("X-Webhook-Signature", $"sha256={signature}");
            request.Headers.Add("X-Webhook-Event", eventName);

            using var response = await client.SendAsync(request, cancellationToken);
            log.StatusCode = (int)response.StatusCode;
            log.Success = response.IsSuccessStatusCode;
            if (!log.Success)
            {
                log.ErrorMessage = $"Non-success status code: {(int)response.StatusCode}";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.Success = false;
            log.ErrorMessage = ex.Message;
        }

        db.WebhookDeliveryLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}
