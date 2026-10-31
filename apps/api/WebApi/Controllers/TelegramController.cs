using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Telegram;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("telegram")]
[Authorize]
[RequireModule(ModuleName.Telegram)]
public class TelegramController(
    ITelegramService telegramService,
    ICurrentUserContext currentUser,
    IValidator<ConnectTelegramAccountRequest> connectValidator,
    IValidator<SendTelegramMessageRequest> sendValidator,
    IValidator<LinkTelegramChatToContactRequest> linkValidator) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<IActionResult> ListAccounts(CancellationToken ct) =>
        Ok(await telegramService.ListAccountsAsync(currentUser.CompanyId!, ct));

    [HttpPost("accounts")]
    public async Task<IActionResult> ConnectAccount(ConnectTelegramAccountRequest request, CancellationToken ct)
    {
        await connectValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await telegramService.ConnectAccountAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send(SendTelegramMessageRequest request, CancellationToken ct)
    {
        await sendValidator.ValidateAndThrowAppAsync(request, ct);
        await telegramService.SendAsync(currentUser.CompanyId!, request, ct);
        return Ok(new { success = true });
    }

    [HttpGet("chat-links")]
    public async Task<IActionResult> ListChatLinks([FromQuery] bool? onlyUnlinked, CancellationToken ct) =>
        Ok(await telegramService.ListChatLinksAsync(currentUser.CompanyId!, onlyUnlinked, ct));

    [HttpPost("chat-links/{id}/link-contact")]
    public async Task<IActionResult> LinkChatToContact(string id, LinkTelegramChatToContactRequest request, CancellationToken ct)
    {
        await linkValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await telegramService.LinkChatToContactAsync(currentUser.CompanyId!, id, request, ct));
    }
}
