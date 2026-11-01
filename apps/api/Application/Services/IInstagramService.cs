using BusinessOS.Application.ViewModels.Instagram;

namespace BusinessOS.Application.Services;

public interface IInstagramService
{
    Task<IReadOnlyList<InstagramAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<InstagramAccountDto> ConnectAccountAsync(string companyId, ConnectInstagramAccountRequest request, CancellationToken cancellationToken = default);
    string GetAuthUrl(string userId, string companyId, string? locale = null);
    // Locale (en/ru/tj) carried in the signed state; falls back to the default for a missing,
    // invalid or expired state so the callback can always redirect somewhere sensible.
    string ResolveLocale(string? state);
    Task<InstagramAccountDto> HandleOAuthCallbackAsync(string code, string state, CancellationToken cancellationToken = default);
    Task DisconnectAccountAsync(string companyId, string actorUserId, string id, CancellationToken cancellationToken = default);
    // Meta "Deauthorize callback": the user removed the app on Instagram. Drops the stored token.
    Task<bool> HandleDeauthorizeAsync(string instagramUserId, CancellationToken cancellationToken = default);
    // Meta "Data deletion request callback": drops the token and Instagram-scoped conversation
    // identities for that account. Contacts/communications stay (they are the company's CRM data).
    Task<bool> HandleDataDeletionAsync(string instagramUserId, CancellationToken cancellationToken = default);
    Task SendAsync(string companyId, SendInstagramMessageRequest request, CancellationToken cancellationToken = default);
    Task ProcessWebhookAsync(string rawJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InstagramConversationLinkDto>> ListConversationLinksAsync(string companyId, bool? onlyUnlinked, CancellationToken cancellationToken = default);
    Task<InstagramConversationLinkDto> LinkConversationToContactAsync(string companyId, string conversationLinkId, LinkInstagramConversationToContactRequest request, CancellationToken cancellationToken = default);
}
