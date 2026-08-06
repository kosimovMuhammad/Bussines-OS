using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Auth;
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
    IValidator<AcceptInviteRequest> acceptInviteValidator,
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
        var tokens = await authService.RegisterAsync(request, ct);
        SetRefreshCookie(tokens.RefreshToken);
        return Ok(new { accessToken = tokens.AccessToken, user = tokens.User });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAppAsync(request, ct);
        var tokens = await authService.LoginAsync(request, ct);
        SetRefreshCookie(tokens.RefreshToken);
        return Ok(new { accessToken = tokens.AccessToken, user = tokens.User });
    }

    [HttpPost("accept-invite")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvite(AcceptInviteRequest request, CancellationToken ct)
    {
        await acceptInviteValidator.ValidateAndThrowAppAsync(request, ct);
        var tokens = await authService.AcceptInviteAsync(request, ct);
        SetRefreshCookie(tokens.RefreshToken);
        return Ok(new { accessToken = tokens.AccessToken, user = tokens.User });
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

        var tokens = await authService.RefreshAsync(rawToken, ct);
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

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await authService.LogoutAllAsync(currentUser.UserId!, ct);
        ClearRefreshCookie();
        return Ok(new { success = true });
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
