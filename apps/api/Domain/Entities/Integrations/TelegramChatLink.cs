using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class TelegramChatLink : IContactBranchScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public long ChatId { get; set; }
    public long TelegramUserId { get; set; }
    public string? TelegramUsername { get; set; }
    public string? TelegramFirstName { get; set; }
    public string? TelegramLastName { get; set; }
    public string? PhoneNumber { get; set; }
    public TelegramLinkMethod LinkMethod { get; set; } = TelegramLinkMethod.Unlinked;
    public DateTime? LinkedAt { get; set; }

    // Holds the most recent inbound message text while ContactId is still null (schema requires
    // Communication.ContactId to be set), so nothing is lost once the chat gets linked to a Contact.
    public string? LastMessagePreview { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
