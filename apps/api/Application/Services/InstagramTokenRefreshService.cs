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

    // Rows created before AppScopedUserId existed stored the app-scoped id in
    // InstagramBusinessAccountId, so no webhook ever matched them. Re-resolve both ids from /me with
    // the account's own token (no user action needed) and re-subscribe webhooks under the right id.
    // Runs on every tick, including shortly after startup.
    private async Task BackfillAccountIdsAsync(CancellationToken cancellationToken)
    {
        var pending = await db.InstagramAccounts.Where(a => a.IsActive && a.AppScopedUserId == null).ToListAsync(cancellationToken);
        foreach (var account in pending)
        {
            var originalId = account.InstagramBusinessAccountId;
            try
            {
                var token = tokenCipher.Decrypt(account.EncryptedAccessToken);
                var info = await client.GetAccountInfoAsync(token, cancellationToken);
                if (string.IsNullOrWhiteSpace(info.UserId))
                {
                    logger.LogWarning("Instagram id backfill: /me returned no user_id for account {AccountId}; reconnect it", account.Id);
                    continue;
                }

                var previousId = account.InstagramBusinessAccountId;
                account.InstagramBusinessAccountId = info.UserId;
                account.AppScopedUserId = info.Id;
                account.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Instagram id backfill: account {AccountId} id {Previous} -> {Current}", account.Id, previousId, info.UserId);

                try
                {
                    await client.SubscribeWebhookAsync(token, info.UserId, cancellationToken);
                }
                catch (ExternalServiceException ex)
                {
                    logger.LogWarning(ex, "Instagram webhook re-subscription failed for account {AccountId}", account.Id);
                }
            }
            catch (ExternalServiceException ex)
            {
                logger.LogWarning(ex, "Instagram id backfill failed for account {AccountId}; reconnect it if this persists", account.Id);
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Instagram id backfill hit a conflict for account {AccountId}; reconnect it", account.Id);
                // Undo the in-memory change so a later SaveChanges in this tick does not retry it.
                account.InstagramBusinessAccountId = originalId;
                account.AppScopedUserId = null;
            }
        }
    }

    public async Task RefreshDueTokensAsync(CancellationToken cancellationToken = default)
    {
        await BackfillAccountIdsAsync(cancellationToken);

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
