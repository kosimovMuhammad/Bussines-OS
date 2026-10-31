using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Services;

public class WhatsAppTokenRefreshService(IAppDbContext db, IWhatsAppClient client, ITokenCipher tokenCipher, ILogger<WhatsAppTokenRefreshService> logger)
    : IWhatsAppTokenRefreshService
{
    // Mirrors InstagramTokenRefreshService.RefreshWindow - see that class for the reasoning.
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromDays(10);

    public async Task RefreshDueTokensAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Add(RefreshWindow);
        var dueAccounts = await db.WhatsAppAccounts
            .Where(a => a.IsActive && a.TokenExpiresAt != null && a.TokenExpiresAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var account in dueAccounts)
        {
            try
            {
                var currentToken = tokenCipher.Decrypt(account.EncryptedAccessToken);
                var refreshed = await client.ExchangeForLongLivedTokenAsync(currentToken, cancellationToken);
                account.EncryptedAccessToken = tokenCipher.Encrypt(refreshed.AccessToken);
                account.TokenExpiresAt = refreshed.ExpiresAt;
                account.UpdatedAt = DateTime.UtcNow;
            }
            catch (ExternalServiceException ex)
            {
                account.IsActive = false;
                logger.LogWarning(ex, "WhatsApp token refresh failed for account {AccountId}; marked inactive", account.Id);
            }
        }

        if (dueAccounts.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
