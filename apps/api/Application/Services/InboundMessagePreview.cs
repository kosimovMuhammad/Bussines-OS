namespace BusinessOS.Application.Services;

// Shared by WhatsAppService/InstagramService/TelegramService so the NewMessage notification
// body never carries a full essay - just enough to recognize the conversation from the bell.
public static class InboundMessagePreview
{
    private const int MaxLength = 120;

    public static string Truncate(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength].TrimEnd() + "…";
    }
}
