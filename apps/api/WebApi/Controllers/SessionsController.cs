using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("sessions")]
[Authorize]
[RequireModule(ModuleName.Sessions)]
public class SessionsController(
    ISessionsService sessionsService,
    IAuthService authService,
    ITokenService tokenService,
    ICurrentUserContext currentUser) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sessionsService.ListAsync(currentUser.UserId!, CurrentSessionId(), ct));

    // RequireAction(Read) override: revoking your own session is self-service security
    // hygiene, not a privileged mutation - every role gets Read by default, same pattern
    // as AuthController.LogoutAll and Users/:id/assignment.
    [HttpDelete("{id}")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Revoke(string id, CancellationToken ct)
    {
        await sessionsService.RevokeAsync(currentUser.UserId!, id, ct);
        return NoContent();
    }

    // Same feature as POST /auth/logout-all (Phase 0), just reachable under /sessions too -
    // reuses AuthService.LogoutAllAsync rather than re-implementing session revocation.
    [HttpDelete("logout-all")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await authService.LogoutAllAsync(currentUser.CompanyId!, currentUser.UserId!, ct);
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/" });
        return NoContent();
    }

    private string? CurrentSessionId()
    {
        var rawToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rawToken))
        {
            return null;
        }

        return tokenService.DecodeRefreshTokenUnsafe(rawToken)?.Jti;
    }
}
