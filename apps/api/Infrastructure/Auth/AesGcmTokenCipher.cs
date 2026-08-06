using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Infrastructure.Auth;

public class AesGcmTokenCipher : ITokenCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmTokenCipher(IConfiguration configuration)
    {
        var raw = configuration["ENCRYPTION_KEY"] ?? throw new InvalidOperationException("ENCRYPTION_KEY is not configured");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
    }

    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSize + TagSize, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        var all = Convert.FromBase64String(ciphertext);
        var nonce = all.AsSpan(0, NonceSize);
        var tag = all.AsSpan(NonceSize, TagSize);
        var cipherBytes = all.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
