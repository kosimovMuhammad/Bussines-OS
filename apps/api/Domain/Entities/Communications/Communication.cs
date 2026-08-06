using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class Communication : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public CommunicationChannel Channel { get; set; }
    public CommunicationDirection Direction { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AttachmentsJson { get; set; }
    public string? ProviderMessageId { get; set; }
    public CommunicationStatus Status { get; set; } = CommunicationStatus.Received;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public AiSummary? AiSummary { get; set; }
}
