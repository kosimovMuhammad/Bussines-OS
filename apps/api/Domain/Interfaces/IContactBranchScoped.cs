using BusinessOS.Domain.Entities;

namespace BusinessOS.Domain.Interfaces;

// Implemented by entities that don't carry a branch of their own but hang off an optional
// Contact (Communication, InstagramConversationLink, TelegramChatLink) - branch visibility
// is derived by joining to Contact.BranchId rather than denormalizing a copy, so it can never
// go stale if a contact's branch is reassigned later.
public interface IContactBranchScoped : ITenantScoped
{
    string? ContactId { get; set; }
    Contact? Contact { get; set; }
}
