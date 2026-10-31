namespace BusinessOS.Application.Services;

public interface IWhatsAppTokenRefreshService
{
    // Mirrors IInstagramTokenRefreshService - renews every active WhatsAppAccount whose
    // long-lived token is close to its ~60-day expiry. A revoked token fails here and the
    // account is marked IsActive = false so the frontend can prompt for reconnection.
    Task RefreshDueTokensAsync(CancellationToken cancellationToken = default);
}
