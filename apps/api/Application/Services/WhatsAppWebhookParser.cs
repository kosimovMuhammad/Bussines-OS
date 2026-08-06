using System.Text.Json;
using BusinessOS.Application.ViewModels.WhatsApp;

namespace BusinessOS.Application.Services;

public static class WhatsAppWebhookParser
{
    public static IReadOnlyList<IncomingWhatsAppMessage> ExtractIncomingMessages(string rawJson)
    {
        var results = new List<IncomingWhatsAppMessage>();

        using var doc = JsonDocument.Parse(rawJson);
        if (!doc.RootElement.TryGetProperty("entry", out var entries))
        {
            return results;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes))
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value))
                {
                    continue;
                }

                var phoneNumberId = value.TryGetProperty("metadata", out var metadata) &&
                    metadata.TryGetProperty("phone_number_id", out var pni)
                    ? pni.GetString() ?? string.Empty
                    : string.Empty;

                if (!value.TryGetProperty("messages", out var messages))
                {
                    continue;
                }

                foreach (var message in messages.EnumerateArray())
                {
                    var from = message.TryGetProperty("from", out var f) ? f.GetString() ?? string.Empty : string.Empty;
                    var id = message.TryGetProperty("id", out var i) ? i.GetString() ?? string.Empty : string.Empty;
                    var text = message.TryGetProperty("text", out var t) && t.TryGetProperty("body", out var body)
                        ? body.GetString() ?? string.Empty
                        : string.Empty;
                    var timestamp = message.TryGetProperty("timestamp", out var ts) && long.TryParse(ts.GetString(), out var unixSeconds)
                        ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime
                        : DateTime.UtcNow;

                    if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    results.Add(new IncomingWhatsAppMessage(id, phoneNumberId, from, text, timestamp));
                }
            }
        }

        return results;
    }
}
