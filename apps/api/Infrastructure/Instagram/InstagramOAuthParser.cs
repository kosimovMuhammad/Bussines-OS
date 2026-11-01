using System.Text.Json;
using BusinessOS.Application.ViewModels.Instagram;

namespace BusinessOS.Infrastructure.Instagram;

// Pure parsing/normalisation helpers for the Instagram Login OAuth responses, kept separate from
// the HTTP client so the quirks below are unit-testable without a network.
public static class InstagramOAuthParser
{
    // Instagram's authorization redirect can leave a trailing "#_" on the code (it is the
    // fragment marker Meta appends to the redirect URL; when it ends up in the value it makes the
    // token exchange fail with "Invalid authorization code"). Strip it, and surrounding spaces.
    public static string NormalizeCode(string code)
    {
        var trimmed = code.Trim();
        const string suffix = "#_";
        return trimmed.EndsWith(suffix, StringComparison.Ordinal) ? trimmed[..^suffix.Length] : trimmed;
    }

    // POST https://api.instagram.com/oauth/access_token answers with one of two shapes:
    //   Instagram Login (business): {"data":[{"access_token":"...","user_id":"...","permissions":"..."}]}
    //   legacy flat shape:          {"access_token":"...","user_id":123}
    // Accept both so a Meta-side change of either kind doesn't break the connect flow.
    public static string ParseShortLivedAccessToken(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var node = doc.RootElement;

        if (node.ValueKind == JsonValueKind.Object && node.TryGetProperty("data", out var data))
        {
            node = data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0 ? data[0] : data;
        }

        if (node.ValueKind != JsonValueKind.Object
            || !node.TryGetProperty("access_token", out var token)
            || token.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(token.GetString()))
        {
            throw new JsonException("No access_token in Instagram token response");
        }

        return token.GetString()!;
    }

    // GET /me?fields=id,user_id,username,name. Parsed by hand (not via the DTO's serializer
    // mapping) so ids are accepted whether Meta sends them as strings or numbers, and so the
    // snake_case "user_id" can never silently go unbound again - that is exactly what made the
    // professional account id come back null and the app-scoped "id" get stored instead.
    public static InstagramUserInfo ParseAccountInfo(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Instagram account info is not an object");
        }

        var id = ReadString(root, "id") ?? throw new JsonException("No id in Instagram account info");
        return new InstagramUserInfo(id, ReadString(root, "user_id"), ReadString(root, "username") ?? string.Empty, ReadString(root, "name"));
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
