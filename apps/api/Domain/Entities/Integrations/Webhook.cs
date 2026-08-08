using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Outbound webhooks - this company's system notifying external URLs on its own events
// (e.g. "deal.created"), distinct from the existing inbound webhook receivers
// (WhatsAppWebhookController/GoogleCalendarWebhookController/PaymentWebhookController),
// which are the opposite direction (external systems notifying this one). SecretEncrypted
// mirrors OAuthConnection's token-at-rest handling (ITokenCipher, AES-256-GCM) - unlike an
// ApiKey's hash-only storage, the plaintext secret is needed again at delivery time to
// compute each request's HMAC signature, so it can't be a one-way hash.
public class Webhook : IActivityLoggable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public string SecretEncrypted { get; set; } = string.Empty;
    public List<string> SubscribedEvents { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
