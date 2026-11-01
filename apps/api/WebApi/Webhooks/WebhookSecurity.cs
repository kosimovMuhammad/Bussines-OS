using System.Security.Cryptography;
using System.Text;

namespace BusinessOS.WebApi.Webhooks;

public static class WebhookSecurity
{
    // Meta webhook payloads are small; 1 MB is far above a realistic batch and still bounds
    // what an anonymous caller can make us buffer before the signature is checked.
    public const int MaxWebhookBodyBytes = 1_048_576;

    // Constant-time string comparison that also hides length differences: both sides are hashed
    // to a fixed width first, so neither content nor length of the expected secret leaks via timing.
    public static bool SecretsEqual(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var a = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    // hub.mode=subscribe + matching hub.verify_token -> echo hub.challenge (Meta GET verification).
    public static bool IsValidVerification(string? mode, string? verifyToken, string? expectedToken) =>
        mode == "subscribe" && SecretsEqual(verifyToken, expectedToken);

    // X-Hub-Signature-256: "sha256=<hex HMAC-SHA256 of the RAW request bytes, keyed with the app secret>".
    public static bool IsValidMetaSignature(byte[] body, string? signatureHeader, string? appSecret)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(signatureHeader)
            || !signatureHeader.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var computed = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        var providedHex = signatureHeader[prefix.Length..].Trim();
        byte[] provided;
        try
        {
            provided = Convert.FromHexString(providedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(provided, computed);
    }

    // The body-size limit itself is enforced by [RequestSizeLimit] on the action (Kestrel answers
    // 413 once the cap is exceeded); this just buffers the raw bytes so the signature is computed
    // over exactly what Meta signed rather than a re-encoded string.
    public static async Task<byte[]> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
