using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Ai;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("ai")]
[Authorize]
[RequireModule(ModuleName.Ai)]
public class AiController(
    IAiService aiService,
    ICurrentUserContext currentUser,
    IValidator<SummarizeRequest> summarizeValidator,
    IValidator<AiQueryRequest> queryValidator,
    IValidator<AiDraftEmailRequest> draftEmailValidator,
    IValidator<AiGenerateQuotationRequest> generateQuotationValidator,
    IValidator<AiForecastRequest> forecastValidator,
    IValidator<AiAssistantMessageRequest> assistantMessageValidator) : ControllerBase
{
    [HttpPost("summarize")]
    public async Task<IActionResult> Summarize(SummarizeRequest request, CancellationToken ct)
    {
        await summarizeValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await aiService.SummarizeAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet("summary/{id}")]
    public async Task<IActionResult> GetSummary(string id, CancellationToken ct) =>
        Ok(await aiService.GetSummaryAsync(currentUser.CompanyId!, id, ct));

    [HttpPost("query")]
    public async Task<IActionResult> Query(AiQueryRequest request, CancellationToken ct)
    {
        await queryValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await aiService.QueryAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet("query/result/{id}")]
    public async Task<IActionResult> GetQueryResult(string id, CancellationToken ct) =>
        Ok(await aiService.GetQueryResultAsync(currentUser.CompanyId!, id, ct));

    [HttpPost("draft-email")]
    public async Task<IActionResult> DraftEmail(AiDraftEmailRequest request, CancellationToken ct)
    {
        await draftEmailValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await aiService.DraftEmailAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet("draft-email/result/{id}")]
    public async Task<IActionResult> GetDraftEmail(string id, CancellationToken ct) =>
        Ok(await aiService.GetDraftEmailAsync(currentUser.CompanyId!, id, ct));

    [HttpPost("generate-quotation")]
    public async Task<IActionResult> GenerateQuotation(AiGenerateQuotationRequest request, CancellationToken ct)
    {
        await generateQuotationValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await aiService.GenerateQuotationAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet("generate-quotation/result/{id}")]
    public async Task<IActionResult> GetQuotation(string id, CancellationToken ct) =>
        Ok(await aiService.GetQuotationAsync(currentUser.CompanyId!, id, ct));

    // Fast enough to answer synchronously (aggregates over Deals/Invoices already in the
    // DB, no LLM call) - unlike the other Ai endpoints above, no job/poll pair needed.
    [HttpPost("forecast")]
    public async Task<IActionResult> Forecast(AiForecastRequest request, CancellationToken ct)
    {
        await forecastValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await aiService.GetForecastAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpPost("assistant/message")]
    public async Task<IActionResult> SendAssistantMessage(AiAssistantMessageRequest request, CancellationToken ct)
    {
        await assistantMessageValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await aiService.SendAssistantMessageAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet("assistant/conversations/{id}")]
    public async Task<IActionResult> GetAssistantConversation(string id, CancellationToken ct) =>
        Ok(await aiService.GetAssistantConversationAsync(currentUser.CompanyId!, id, ct));
}
