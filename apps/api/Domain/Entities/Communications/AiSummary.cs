using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class AiSummary
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CommunicationId { get; set; } = string.Empty;
    public Communication Communication { get; set; } = null!;
    public string Summary { get; set; } = string.Empty;
    public Sentiment? Sentiment { get; set; }
    public string? ActionItemsJson { get; set; }
    public DateTime? NextFollowUp { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
