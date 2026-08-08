using BusinessOS.Application.ViewModels.Sessions;

namespace BusinessOS.Application.Services;

public interface ISessionsService
{
    // currentSessionId (the caller's own refresh token jti, decoded from their cookie by
    // the controller) is optional purely for the IsCurrent flag - omitting it just means
    // every row comes back with IsCurrent=false.
    Task<IReadOnlyList<SessionDto>> ListAsync(string userId, string? currentSessionId, CancellationToken cancellationToken = default);
    Task RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
}
