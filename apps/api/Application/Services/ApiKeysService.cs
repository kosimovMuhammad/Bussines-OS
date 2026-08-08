using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.ApiKeys;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class ApiKeysService(IAppDbContext db) : IApiKeysService
{
    private const string KeyPrefixTag = "bos_";
    private const int PrefixDisplayLength = 12;

    public async Task<IReadOnlyList<ApiKeyDto>> ListAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var keys = await db.ApiKeys.Include(k => k.CreatedByUser).OrderByDescending(k => k.CreatedAt).ToListAsync(cancellationToken);
        return keys.Select(ToDto).ToList();
    }

    public async Task<CreateApiKeyResponse> CreateAsync(string companyId, string actorUserId, CreateApiKeyRequest request, CancellationToken cancellationToken = default)
    {
        var rawKey = GenerateRawKey();
        var apiKey = new ApiKey
        {
            CompanyId = companyId,
            Name = request.Name,
            KeyPrefix = rawKey[..PrefixDisplayLength],
            KeyHash = Hash(rawKey),
            CreatedByUserId = actorUserId,
            ExpiresAt = request.ExpiresAt,
        };

        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateApiKeyResponse(apiKey.Id, apiKey.Name, rawKey, apiKey.KeyPrefix, apiKey.ExpiresAt, apiKey.CreatedAt);
    }

    public async Task<ApiKeyDto> UpdateAsync(string companyId, string id, UpdateApiKeyRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await db.ApiKeys.Include(k => k.CreatedByUser).SingleOrDefaultAsync(k => k.Id == id, cancellationToken)
            ?? throw new NotFoundException("API key not found");

        if (request.Name is not null)
        {
            apiKey.Name = request.Name;
        }

        if (request.IsActive is not null)
        {
            apiKey.IsActive = request.IsActive.Value;
        }

        apiKey.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(apiKey);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var apiKey = await db.ApiKeys.SingleOrDefaultAsync(k => k.Id == id, cancellationToken)
            ?? throw new NotFoundException("API key not found");

        db.ApiKeys.Remove(apiKey);
        await db.SaveChangesAsync(cancellationToken);
    }

    // High-entropy random value, not a low-entropy user-chosen secret - SHA-256 (fast) is
    // the right tool here, not Argon2 (deliberately slow, meant for password verification).
    private static string GenerateRawKey() => KeyPrefixTag + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string rawKey) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));

    private static ApiKeyDto ToDto(ApiKey k) => new(
        k.Id, k.Name, k.KeyPrefix, k.IsActive, k.CreatedByUserId, k.CreatedByUser?.Name, k.LastUsedAt, k.ExpiresAt, k.CreatedAt);
}
