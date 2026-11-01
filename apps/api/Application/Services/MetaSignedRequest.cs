using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BusinessOS.Application.Services;

// Meta's "signed_request" (deauthorize / data deletion callbacks):
//   base64url(HMAC-SHA256(payloadPart, appSecret)) + "." + base64url(json payload)
// The signature covers the *encoded* payload string exactly as received.
public static class MetaSignedRequest
{
    // Returns the payload's user_id only if the signature is valid for appSecret.
    public static bool TryGetUserId(string? signedRequest, string? appSecret, out string userId)
    {
        userId = string.Empty;
        if (string.IsNullOrEmpty(signedRequest) || string.IsNullOrEmpty(appSecret))
        {
            return false;
        }

        var parts = signedRequest.Split('.', 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return false;
        }

        try
        {
            var providedSignature = Base64UrlDecode(parts[0]);
            var expectedSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes(parts[1]));
            if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
            {
                return false;
            }

            using var doc = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("algorithm", out var algorithm)
                && !string.Equals(algorithm.GetString(), "HMAC-SHA256", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!root.TryGetProperty("user_id", out var id))
            {
                return false;
            }

            var value = id.ValueKind == JsonValueKind.String ? id.GetString() : id.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            userId = value;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }

    // Data-deletion confirmation codes are stateless: "<nonce>.<hmac(nonce)>". The status page
    // can then confirm a code is one we issued without storing a deletion-request table.
    public static string CreateConfirmationCode(string appSecret)
    {
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        return $"{nonce}.{ConfirmationMac(nonce, appSecret)}";
    }

    public static bool IsValidConfirmationCode(string? code, string? appSecret)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(appSecret))
        {
            return false;
        }

        var parts = code.Split('.', 2);
        if (parts.Length != 2 || parts[0].Length != 16)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(parts[1]), Encoding.UTF8.GetBytes(ConfirmationMac(parts[0], appSecret)));
    }

    private static string ConfirmationMac(string nonce, string appSecret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes("ig-data-deletion:" + nonce)))[..32];

    // For tests / tooling.
    public static string Create(string userId, string appSecret)
    {
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { algorithm = "HMAC-SHA256", issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), user_id = userId })));
        var signature = Base64UrlEncode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), Encoding.UTF8.GetBytes(payload)));
        return $"{signature}.{payload}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
