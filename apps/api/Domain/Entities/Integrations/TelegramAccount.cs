using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class TelegramAccount : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string EncryptedBotToken { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
