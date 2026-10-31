using BusinessOS.Application.ViewModels.Instagram;

namespace BusinessOS.Application.Services;

public interface IInstagramService
{
    Task<IReadOnlyList<InstagramAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<InstagramAccountDto> ConnectAccountAsync(string companyId, ConnectInstagramAccountRequest request, CancellationToken cancellationToken = default);
    string GetAuthUrl(string userId, string companyId);
    Task<InstagramAccountDto> HandleOAuthCallbackAsync(string code, string state, CancellationToken cancellationToken = default);
    Task SendAsync(string companyId, SendInstagramMessageRequest request, CancellationToken cancellationToken = default);
    Task ProcessWebhookAsync(string rawJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InstagramConversationLinkDto>> ListConversationLinksAsync(string companyId, bool? onlyUnlinked, CancellationToken cancellationToken = default);
    Task<InstagramConversationLinkDto> LinkConversationToContactAsync(string companyId, string conversationLinkId, LinkInstagramConversationToContactRequest request, CancellationToken cancellationToken = default);
}
