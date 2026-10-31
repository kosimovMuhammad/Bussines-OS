using System.Text.Json.Serialization;

namespace BusinessOS.Application.ViewModels.Instagram;

public record InstagramWebhookPayload(
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("entry")] List<InstagramWebhookEntry>? Entry);

public record InstagramWebhookEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("time")] long Time,
    [property: JsonPropertyName("messaging")] List<InstagramMessagingEvent>? Messaging);

public record InstagramMessagingEvent(
    [property: JsonPropertyName("sender")] InstagramSender Sender,
    [property: JsonPropertyName("recipient")] InstagramRecipient Recipient,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("message")] InstagramMessage? Message);

// The recipient of an inbound message is our own Instagram Business Account - this is how we
// resolve which tenant (InstagramAccount.InstagramBusinessAccountId) a webhook call belongs to.
public record InstagramSender([property: JsonPropertyName("id")] string Id);

public record InstagramRecipient([property: JsonPropertyName("id")] string Id);

public record InstagramMessage(
    [property: JsonPropertyName("mid")] string Mid,
    [property: JsonPropertyName("text")] string? Text);
