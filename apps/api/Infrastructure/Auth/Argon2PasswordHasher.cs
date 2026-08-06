using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Abstraction;
using Konscious.Security.Cryptography;

namespace BusinessOS.Infrastructure.Auth;

// Argon2id, matching the parameters the original Node "argon2" package defaults to
// (64 MiB memory, 3 iterations, 4-way parallelism), encoded PHC-string style so the
// hash is self-describing and doesn't need its parameters stored separately.
public class Argon2PasswordHasher : IPasswordHasher
{
    private const int MemorySizeKb = 65536;
    private const int Iterations = 3;
    private const int Parallelism = 4;
    private const int SaltLength = 16;
    private const int HashLength = 32;

    public Task<string> HashAsync(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = ComputeHash(password, salt);
        var encoded = $"$argon2id$v=19$m={MemorySizeKb},t={Iterations},p={Parallelism}$" +
                      $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        return Task.FromResult(encoded);
    }

    public Task<bool> VerifyAsync(string hash, string password)
    {
        var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
        {
            return Task.FromResult(false);
        }

        var salt = Convert.FromBase64String(parts[3]);
        var expected = Convert.FromBase64String(parts[4]);
        var actual = ComputeHash(password, salt, expected.Length);

        return Task.FromResult(CryptographicOperations.FixedTimeEquals(actual, expected));
    }

    private static byte[] ComputeHash(string password, byte[] salt, int length = HashLength)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            MemorySize = MemorySizeKb,
            Iterations = Iterations,
        };
        return argon2.GetBytes(length);
    }
}
