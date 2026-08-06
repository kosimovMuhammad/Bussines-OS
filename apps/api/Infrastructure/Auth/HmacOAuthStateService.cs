using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Infrastructure.Auth;

public class HmacOAuthStateService : IOAuthStateService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly byte[] _secret;

    public HmacOAuthStateService(IConfiguration configuration)
    {
        var raw = configuration["JWT_ACCESS_SECRET"] ?? throw new InvalidOperationException("JWT_ACCESS_SECRET is not configured");
        _secret = Encoding.UTF8.GetBytes(raw);
    }

    public string Create(string userId, string companyId)
    {
        var payload = JsonSerializer.Serialize(new
        {
            userId,
            companyId,
            exp = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds(),
        });

        var payloadBytes = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signature = Base64UrlEncode(Sign(payloadBytes));
        return $"{payloadBytes}.{signature}";
    }

    public OAuthStatePayload? Validate(string state)
    {
        var parts = state.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var expectedSignature = Base64UrlEncode(Sign(parts[0]));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedSignature), Encoding.UTF8.GetBytes(parts[1])))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var exp = root.GetProperty("exp").GetInt64();
            if (DateTimeOffset.FromUnixTimeSeconds(exp) < DateTimeOffset.UtcNow)
            {
                return null;
            }

            return new OAuthStatePayload(root.GetProperty("userId").GetString()!, root.GetProperty("companyId").GetString()!);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private byte[] Sign(string payloadBase64) => Sign(Encoding.UTF8.GetBytes(payloadBase64));

    private byte[] Sign(byte[] data)
    {
        using var hmac = new HMACSHA256(_secret);
        return hmac.ComputeHash(data);
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
