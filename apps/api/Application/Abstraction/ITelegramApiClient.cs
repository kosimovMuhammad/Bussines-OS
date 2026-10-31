using BusinessOS.Application.ViewModels.Telegram;

namespace BusinessOS.Application.Abstraction;

public interface ITelegramApiClient
{
    Task<TelegramUser> GetMeAsync(string botToken, CancellationToken cancellationToken = default);

    Task<long?> SendMessageAsync(string botToken, long chatId, string text, CancellationToken cancellationToken = default);

    Task SendMessageWithContactRequestAsync(string botToken, long chatId, string text, string buttonText, CancellationToken cancellationToken = default);

    // Telegram echoes secretToken back on every webhook call as the "X-Telegram-Bot-Api-Secret-Token"
    // header - that's what lets TelegramWebhookController verify a call actually came from Telegram.
    Task SetWebhookAsync(string botToken, string webhookUrl, string secretToken, CancellationToken cancellationToken = default);
}
