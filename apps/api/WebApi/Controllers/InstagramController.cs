using BusinessOS.Application.Abstraction;
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
    [HttpGet("connect/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ConnectCallback([FromQuery] string code, [FromQuery] string state, CancellationToken ct)
    {
        var frontendUrl = (configuration["FRONTEND_URL"] ?? "http://localhost:5173").TrimEnd('/');
        try
        {
            var account = await instagramService.HandleOAuthCallbackAsync(code, state, ct);
            return Redirect($"{frontendUrl}/en/instagram?connected=true&username={Uri.EscapeDataString(account.Username)}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Instagram OAuth callback failed");
            return Redirect($"{frontendUrl}/en/instagram?connected=false&error={Uri.EscapeDataString(ex.Message)}");
        }
    }

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
