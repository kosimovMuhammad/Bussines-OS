using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class AiAssistantMessage : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string ConversationId { get; set; } = string.Empty;
    public AiAssistantConversation Conversation { get; set; } = null!;
    public AiAssistantRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public AiJobStatus Status { get; set; } = AiJobStatus.Completed;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
