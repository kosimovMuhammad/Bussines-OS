using BusinessOS.Application.ViewModels.Auth;

namespace BusinessOS.Application.Services;

public interface IAuthService
{
    Task<RegistrationPendingDto> RegisterAsync(RegisterRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<AuthTokens> VerifyEmailAsync(VerifyEmailRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<RegistrationPendingDto> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokens> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<AuthTokens> LoginWithCodeAsync(LoginWithCodeRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<RegistrationPendingDto> ResendInviteCodeAsync(ResendInviteCodeRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokens> RefreshAsync(string rawRefreshToken, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken = default);
    Task LogoutAllAsync(string companyId, string userId, CancellationToken cancellationToken = default);
}
