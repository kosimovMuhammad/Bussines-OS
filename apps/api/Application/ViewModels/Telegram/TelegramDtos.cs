using BusinessOS.Domain.Enums;
using FluentValidation;

namespace BusinessOS.Application.ViewModels.Telegram;

public record ConnectTelegramAccountRequest(string BotToken);

public record TelegramAccountDto(string Id, string CompanyId, string BotUsername, bool IsActive, DateTime CreatedAt);

public record SendTelegramMessageRequest(string ContactId, string Message);

public record TelegramChatLinkDto(
    string Id,
    string CompanyId,
    string? ContactId,
    long ChatId,
    long TelegramUserId,
    string? TelegramUsername,
    string? TelegramFirstName,
    string? TelegramLastName,
    string? PhoneNumber,
    TelegramLinkMethod LinkMethod,
    DateTime? LinkedAt,
    string? LastMessagePreview,
    DateTime CreatedAt);

public record LinkTelegramChatToContactRequest(string ContactId);

public class ConnectTelegramAccountRequestValidator : AbstractValidator<ConnectTelegramAccountRequest>
{
    public ConnectTelegramAccountRequestValidator()
    {
        RuleFor(x => x.BotToken).NotEmpty();
    }
}

public class SendTelegramMessageRequestValidator : AbstractValidator<SendTelegramMessageRequest>
{
    public SendTelegramMessageRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty();
    }
}

public class LinkTelegramChatToContactRequestValidator : AbstractValidator<LinkTelegramChatToContactRequest>
{
    public LinkTelegramChatToContactRequestValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty();
    }
}
