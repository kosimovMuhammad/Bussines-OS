using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// Append-only delivery ledger, direct CompanyId (not just reachable via Webhook) so
// GET /webhooks/logs can query company-wide without joining - same reasoning as
// InventoryMovement's direct CompanyId. Url is denormalized (copied at delivery time, not
// looked up live) so a log entry still shows where an attempt actually went even if the
// Webhook's Url is edited or the Webhook itself is deleted later.
public class WebhookDeliveryLog : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? WebhookId { get; set; }
    public Webhook? Webhook { get; set; }
    public string Event { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
