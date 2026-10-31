namespace BusinessOS.Application.Services;

public interface IInstagramTokenRefreshService
{
    // Renews every active InstagramAccount whose long-lived token is close to its ~60-day
    // expiry - see InstagramTokenRefreshHostedService for the schedule. A token Meta has
    // actually revoked (user removed app access, etc.) fails here and the account is marked
    // IsActive = false so the frontend can prompt for reconnection.
    Task RefreshDueTokensAsync(CancellationToken cancellationToken = default);
}
