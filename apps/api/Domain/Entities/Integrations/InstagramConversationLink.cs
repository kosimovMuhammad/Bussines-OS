using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class InstagramConversationLink : IContactBranchScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? ContactId { get; set; }
    public Contact? Contact { get; set; }

    // Instagram-Scoped ID - identifies a user within this app+Instagram Business account
    // pair. Not a username or phone number, and Instagram does not expose either of those
    // for messaging contacts, so this is the only stable identifier we get.
    public string InstagramScopedId { get; set; } = string.Empty;
    public string? InstagramUsername { get; set; }
    public InstagramLinkMethod LinkMethod { get; set; } = InstagramLinkMethod.Unlinked;
    public DateTime? LinkedAt { get; set; }

    // Holds the most recent inbound message text while ContactId is still null (schema requires
    // Communication.ContactId to be set), so nothing is lost once the conversation gets linked
    // to a Contact - same reasoning as TelegramChatLink.LastMessagePreview.
    public string? LastMessagePreview { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
