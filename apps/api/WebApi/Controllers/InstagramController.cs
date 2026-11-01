using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Instagram;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("instagram")]
[Authorize]
[RequireModule(ModuleName.Instagram)]
public class InstagramController(
    IInstagramService instagramService,
    ICurrentUserContext currentUser,
    IConfiguration configuration,
    ILogger<InstagramController> logger,
    IValidator<ConnectInstagramAccountRequest> connectValidator,
    IValidator<SendInstagramMessageRequest> sendValidator,
    IValidator<LinkInstagramConversationToContactRequest> linkValidator) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<IActionResult> ListAccounts(CancellationToken ct) =>
        Ok(await instagramService.ListAccountsAsync(currentUser.CompanyId!, ct));

    [HttpPost("accounts")]
    public async Task<IActionResult> ConnectAccount(ConnectInstagramAccountRequest request, CancellationToken ct)
    {
        await connectValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await instagramService.ConnectAccountAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpGet("connect")]
    public IActionResult Connect() =>
        Ok(new OAuthUrlDto(instagramService.GetAuthUrl(currentUser.UserId!, currentUser.CompanyId!)));

    // This is a browser redirect target (Instagram itself navigates the user's browser here),
    // not an API call from our own frontend JS - so on both success and failure it redirects
    // back into the app instead of returning raw JSON/error the user would otherwise see
    // rendered as a bare page.
    //
    // Query parameters are all optional on purpose: when the user presses "Cancel" on the consent
    // screen Instagram redirects with ?error=access_denied&error_reason=user_denied&
    // error_description=... and NO code/state - a non-nullable `code` would turn that into a bare
    // 400 JSON page instead of a redirect back into the app.
    //
    // Failure redirects carry a stable machine-readable `error` code (the frontend maps it to its
    // own translated text) plus a human `message`. Internal exception text is never exposed.
    [HttpGet("connect/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ConnectCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_reason")] string? errorReason,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken ct)
    {
        var frontendUrl = (configuration["FRONTEND_URL"] ?? "http://localhost:5173").TrimEnd('/');

        if (!string.IsNullOrEmpty(error))
        {
            logger.LogInformation("Instagram OAuth callback returned error={Error} reason={Reason}", error, errorReason);
            return error == "access_denied"
                ? FailureRedirect(frontendUrl, "access_denied", "Instagram access was not granted.")
                : FailureRedirect(frontendUrl, "provider_error", errorDescription ?? "Instagram reported an error.");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return FailureRedirect(frontendUrl, "invalid_callback", "The Instagram callback was incomplete. Please try again.");
        }

        try
        {
            var account = await instagramService.HandleOAuthCallbackAsync(code, state, ct);
            return Redirect($"{frontendUrl}/en/instagram?connected=true&username={Uri.EscapeDataString(account.Username)}");
        }
        catch (UnauthorizedAppException)
        {
            return FailureRedirect(frontendUrl, "invalid_state", "The connection request expired. Please start again.");
        }
        catch (ConflictException ex)
        {
            return FailureRedirect(frontendUrl, "already_connected", ex.Message);
        }
        catch (ExternalServiceException ex)
        {
            logger.LogWarning(ex, "Instagram OAuth callback failed at the provider");
            return FailureRedirect(frontendUrl, "provider_error", "Instagram rejected the request. Please try again.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Instagram OAuth callback failed");
            return FailureRedirect(frontendUrl, "unknown", "Something went wrong while connecting Instagram.");
        }
    }

    private RedirectResult FailureRedirect(string frontendUrl, string errorCode, string message) =>
        Redirect($"{frontendUrl}/en/instagram?connected=false&error={Uri.EscapeDataString(errorCode)}&message={Uri.EscapeDataString(message)}");

    [HttpPost("send")]
    public async Task<IActionResult> Send(SendInstagramMessageRequest request, CancellationToken ct)
    {
        await sendValidator.ValidateAndThrowAppAsync(request, ct);
        await instagramService.SendAsync(currentUser.CompanyId!, request, ct);
        return Ok(new { success = true });
    }

    [HttpGet("conversation-links")]
    public async Task<IActionResult> ListConversationLinks([FromQuery] bool? onlyUnlinked, CancellationToken ct) =>
        Ok(await instagramService.ListConversationLinksAsync(currentUser.CompanyId!, onlyUnlinked, ct));

    [HttpPost("conversation-links/{id}/link-contact")]
    public async Task<IActionResult> LinkConversationToContact(string id, LinkInstagramConversationToContactRequest request, CancellationToken ct)
    {
        await linkValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await instagramService.LinkConversationToContactAsync(currentUser.CompanyId!, id, request, ct));
    }
}
