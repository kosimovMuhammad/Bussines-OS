using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class AiAssistantConversation : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AiAssistantMessage> Messages { get; set; } = new List<AiAssistantMessage>();
}
