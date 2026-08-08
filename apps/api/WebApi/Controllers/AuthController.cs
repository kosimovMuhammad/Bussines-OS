using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Auth;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    IAuthService authService,
    ICurrentUserContext currentUser,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<LoginWithCodeRequest> loginWithCodeValidator,
    IValidator<VerifyEmailRequest> verifyEmailValidator,
    IValidator<ResendVerificationRequest> resendVerificationValidator,
    IValidator<ResendInviteCodeRequest> resendInviteCodeValidator,
    IWebHostEnvironment env,
    IConfiguration configuration) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";

    // Secure cookies require HTTPS; local dev runs on plain http://localhost, so a
    // hardcoded Secure=true silently drops the Set-Cookie header in Chrome, breaking
    // refresh entirely. Defaults follow the environment but stay overridable via env
    // vars in case prod terminates TLS somewhere that needs different values.
    private bool CookieSecure => bool.TryParse(configuration["COOKIE_SECURE"], out var value)
        ? value
        : !env.IsDevelopment();

    private SameSiteMode CookieSameSite => Enum.TryParse<SameSiteMode>(configuration["COOKIE_SAMESITE"], true, out var value)
        ? value
        : (env.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None);

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        await registerValidator.ValidateAndThrowAppAsync(request, ct);
        var pending = await authService.RegisterAsync(request, GetClientIp(), GetUserAgent(), ct);
        return Ok(pending);
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        await verifyEmailValidator.ValidateAndThrowAppAsync(request, ct);
        var tokens = await authService.VerifyEmailAsync(request, GetClientIp(), GetUserAgent(), ct);
        SetRefreshCookie(tokens.RefreshToken);
        return Ok(new { accessToken = tokens.AccessToken, user = tokens.User });
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        await resendVerificationValidator.ValidateAndThrowAppAsync(request, ct);
        var pending = await authService.ResendVerificationAsync(request, ct);
        return Ok(pending);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAppAsync(request, ct);
        var tokens = await authService.LoginAsync(request, GetClientIp(), GetUserAgent(), ct);
        SetRefreshCookie(tokens.RefreshToken);
        return Ok(new { accessToken = tokens.AccessToken, user = tokens.User });
    }

    [HttpPost("login-code")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LoginWithCode(LoginWithCodeRequest request, CancellationToken ct)
    {
        await loginWithCodeValidator.ValidateAndThrowAppAsync(request, ct);
        var tokens = await authService.LoginWithCodeAsync(request, GetClientIp(), GetUserAgent(), ct);
        SetRefreshCookie(tokens.RefreshToken);
        return Ok(new { accessToken = tokens.AccessToken, user = tokens.User });
    }

    [HttpPost("resend-invite-code")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendInviteCode(ResendInviteCodeRequest request, CancellationToken ct)
    {
        await resendInviteCodeValidator.ValidateAndThrowAppAsync(request, ct);
        var pending = await authService.ResendInviteCodeAsync(request, ct);
        return Ok(pending);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var rawToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rawToken))
        {
            return Unauthorized(new { statusCode = 401, message = "No refresh token", error = "Unauthorized" });
        }

        var tokens = await authService.RefreshAsync(rawToken, GetClientIp(), GetUserAgent(), ct);
        SetRefreshCookie(tokens.RefreshToken);
        return Ok(new { accessToken = tokens.AccessToken, user = tokens.User });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(rawToken))
        {
            await authService.LogoutAsync(rawToken, ct);
        }

        ClearRefreshCookie();
        return Ok(new { success = true });
    }

    // RequireAction(Read) override: logging out of every session must be available to every
    // authenticated role, not just the roles the (currently coarse, all-or-nothing) RolePermissions
    // matrix grants Create/Update/Delete to on the Profile module. Same pattern as
    // PlansController/ReportsController's platform-admin-gated mutations.
    [HttpPost("logout-all")]
    [Authorize]
    [RequireModule(ModuleName.Profile)]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await authService.LogoutAllAsync(currentUser.CompanyId!, currentUser.UserId!, ct);
        ClearRefreshCookie();
        return Ok(new { success = true });
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent()
    {
        var value = Request.Headers.UserAgent.ToString();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private void SetRefreshCookie(string token) => Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
    {
        HttpOnly = true,
        Secure = CookieSecure,
        SameSite = CookieSameSite,
        Path = "/",
        MaxAge = TimeSpan.FromDays(30),
    });

    private void ClearRefreshCookie() => Response.Cookies.Delete(RefreshCookieName, new CookieOptions
    {
        Path = "/",
        Secure = CookieSecure,
        SameSite = CookieSameSite,
    });
}
