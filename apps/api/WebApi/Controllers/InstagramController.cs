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

    // `locale` (en/ru/tj) is signed into the OAuth state so the callback can send the user back to
    // /{locale}/instagram in the language they started in. Unknown values fall back to "en".
    [HttpGet("connect")]
    public IActionResult Connect([FromQuery] string? locale) =>
        Ok(new OAuthUrlDto(instagramService.GetAuthUrl(currentUser.UserId!, currentUser.CompanyId!, locale)));

    [HttpDelete("accounts/{id}")]
    public async Task<IActionResult> DisconnectAccount(string id, CancellationToken ct)
    {
        await instagramService.DisconnectAccountAsync(currentUser.CompanyId!, currentUser.UserId!, id, ct);
        return NoContent();
    }

    // Meta "Deauthorize callback URL": POST application/x-www-form-urlencoded with signed_request.
    // Public by necessity, so authenticity rests solely on the HMAC with the Instagram app secret.
    [HttpPost("deauthorize")]
    [AllowAnonymous]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Deauthorize([FromForm(Name = "signed_request")] string? signedRequest, CancellationToken ct)
    {
        if (!MetaSignedRequest.TryGetUserId(signedRequest, configuration["INSTAGRAM_APP_SECRET"], out var userId))
        {
            logger.LogWarning("Instagram deauthorize rejected: invalid signed_request");
            return BadRequest(new { error = "invalid_signed_request" });
        }

        await instagramService.HandleDeauthorizeAsync(userId, ct);
        return Ok();
    }

    // Meta "Data deletion request URL": must answer JSON { url, confirmation_code }, where url is a
    // page the user can open to check the request's status.
    [HttpPost("data-deletion")]
    [AllowAnonymous]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> DataDeletion([FromForm(Name = "signed_request")] string? signedRequest, CancellationToken ct)
    {
        var appSecret = configuration["INSTAGRAM_APP_SECRET"];
        if (!MetaSignedRequest.TryGetUserId(signedRequest, appSecret, out var userId))
        {
            logger.LogWarning("Instagram data deletion rejected: invalid signed_request");
            return BadRequest(new { error = "invalid_signed_request" });
        }

        await instagramService.HandleDataDeletionAsync(userId, ct);

        var code = MetaSignedRequest.CreateConfirmationCode(appSecret!);
        var publicUrl = (configuration["PUBLIC_URL"] ?? string.Empty).TrimEnd('/');
        return Ok(new
        {
            url = $"{publicUrl}/instagram/data-deletion/status?code={Uri.EscapeDataString(code)}",
            confirmation_code = code,
        });
    }

    // Human-readable status page opened from the data-deletion `url`. Stateless: a code is valid
    // iff we signed it, and deletion is performed synchronously before the code is issued.
    [HttpGet("data-deletion/status")]
    [AllowAnonymous]
    public ContentResult DataDeletionStatus([FromQuery] string? code)
    {
        var valid = MetaSignedRequest.IsValidConfirmationCode(code, configuration["INSTAGRAM_APP_SECRET"]);
        var text = valid
            ? $"Your Instagram data deletion request (confirmation code {System.Net.WebUtility.HtmlEncode(code)}) has been completed."
            : "Unknown or invalid confirmation code.";
        return Content($"<!doctype html><html><head><meta charset=\"utf-8\"><title>Data deletion</title></head><body><p>{text}</p></body></html>", "text/html");
    }

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
        // Locale comes from the signed state (access_denied redirects include it too); an invalid
        // or expired state just falls back to the default locale.
        var pageUrl = $"{frontendUrl}/{instagramService.ResolveLocale(state)}/instagram";

        if (!string.IsNullOrEmpty(error))
        {
            logger.LogInformation("Instagram OAuth callback returned error={Error} reason={Reason}", error, errorReason);
            return error == "access_denied"
                ? FailureRedirect(pageUrl, "access_denied", "Instagram access was not granted.")
                : FailureRedirect(pageUrl, "provider_error", errorDescription ?? "Instagram reported an error.");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return FailureRedirect(pageUrl, "invalid_callback", "The Instagram callback was incomplete. Please try again.");
        }

        try
        {
            var account = await instagramService.HandleOAuthCallbackAsync(code, state, ct);
            return Redirect($"{pageUrl}?connected=true&username={Uri.EscapeDataString(account.Username)}");
        }
        catch (UnauthorizedAppException)
        {
            return FailureRedirect(pageUrl, "invalid_state", "The connection request expired. Please start again.");
        }
        catch (ConflictException ex)
        {
            return FailureRedirect(pageUrl, "already_connected", ex.Message);
        }
        catch (ExternalServiceException ex)
        {
            logger.LogWarning(ex, "Instagram OAuth callback failed at the provider");
            return FailureRedirect(pageUrl, "provider_error", "Instagram rejected the request. Please try again.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Instagram OAuth callback failed");
            return FailureRedirect(pageUrl, "unknown", "Something went wrong while connecting Instagram.");
        }
    }

    private RedirectResult FailureRedirect(string pageUrl, string errorCode, string message) =>
        Redirect($"{pageUrl}?connected=false&error={Uri.EscapeDataString(errorCode)}&message={Uri.EscapeDataString(message)}");

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
