using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Services;

public class InstagramTokenRefreshService(IAppDbContext db, IInstagramApiClient client, ITokenCipher tokenCipher, ILogger<InstagramTokenRefreshService> logger)
    : IInstagramTokenRefreshService
{
    // Refresh well before the ~60-day expiry (Meta requires the token be at least 24h old to
    // refresh, so this window just needs to be comfortably wider than the tick interval) -
    // gives plenty of retry headroom if Meta is briefly unavailable on any given tick.
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromDays(10);

    public async Task RefreshDueTokensAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Add(RefreshWindow);
        var dueAccounts = await db.InstagramAccounts
            .Where(a => a.IsActive && a.TokenExpiresAt != null && a.TokenExpiresAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var account in dueAccounts)
        {
            try
            {
                var currentToken = tokenCipher.Decrypt(account.EncryptedAccessToken);
                var refreshed = await client.RefreshLongLivedTokenAsync(currentToken, cancellationToken);
                account.EncryptedAccessToken = tokenCipher.Encrypt(refreshed.AccessToken);
                account.TokenExpiresAt = refreshed.ExpiresAt;
                account.UpdatedAt = DateTime.UtcNow;
            }
            catch (ExternalServiceException ex)
            {
                // Meta rejected the refresh - almost always because the user revoked the app's
                // access (or the token expired outright). Nothing left to retry automatically;
                // mark it so the frontend can prompt whoever owns this account to reconnect via
                // the OAuth flow.
                account.IsActive = false;
                logger.LogWarning(ex, "Instagram token refresh failed for account {AccountId}; marked inactive", account.Id);
            }
        }

        if (dueAccounts.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
