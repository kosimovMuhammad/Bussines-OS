using BusinessOS.Application.ViewModels.Auth;

namespace BusinessOS.Application.Services;

public interface IAuthService
{
    Task<AuthTokens> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokens> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokens> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken = default);
    Task LogoutAllAsync(string userId, CancellationToken cancellationToken = default);
}
