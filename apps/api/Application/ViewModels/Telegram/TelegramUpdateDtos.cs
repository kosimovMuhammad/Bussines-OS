using System.Text.Json.Serialization;

namespace BusinessOS.Application.ViewModels.Telegram;

public record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message);

public record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("date")] long Date,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("from")] TelegramUser? From,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("contact")] TelegramContact? Contact);

public record TelegramChat(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string Type);

// Also used as the response shape of the Bot API's getMe method.
public record TelegramUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("is_bot")] bool IsBot,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("username")] string? Username);

public record TelegramContact(
    [property: JsonPropertyName("phone_number")] string PhoneNumber,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("user_id")] long? UserId);
