using BusinessOS.Application.ViewModels.Telegram;

namespace BusinessOS.Application.Services;

public interface ITelegramService
{
    Task<IReadOnlyList<TelegramAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<TelegramAccountDto> ConnectAccountAsync(string companyId, ConnectTelegramAccountRequest request, CancellationToken cancellationToken = default);
    Task SendAsync(string companyId, SendTelegramMessageRequest request, CancellationToken cancellationToken = default);
    Task ProcessWebhookAsync(string webhookSecret, string rawJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelegramChatLinkDto>> ListChatLinksAsync(string companyId, bool? onlyUnlinked, CancellationToken cancellationToken = default);
    Task<TelegramChatLinkDto> LinkChatToContactAsync(string companyId, string chatLinkId, LinkTelegramChatToContactRequest request, CancellationToken cancellationToken = default);
}
