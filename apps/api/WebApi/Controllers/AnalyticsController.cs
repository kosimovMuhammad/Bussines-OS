using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("dashboard")]
[Authorize]
[RequireModule(ModuleName.Dashboard)]
public class AnalyticsController(IAnalyticsService analyticsService, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken ct) =>
        Ok(await analyticsService.GetOverviewAsync(currentUser.CompanyId!, ct));

    [HttpGet("sales")]
    [RequireModule(ModuleName.Analytics)]
    public async Task<IActionResult> Sales(CancellationToken ct) =>
        Ok(await analyticsService.GetSalesAsync(currentUser.CompanyId!, ct));

    [HttpGet("revenue")]
    [RequireModule(ModuleName.Analytics)]
    public async Task<IActionResult> Revenue(CancellationToken ct) =>
        Ok(await analyticsService.GetRevenueAsync(currentUser.CompanyId!, ct));

    [HttpGet("tasks")]
    [RequireModule(ModuleName.Analytics)]
    public async Task<IActionResult> Tasks(CancellationToken ct) =>
        Ok(await analyticsService.GetTaskStatsAsync(currentUser.CompanyId!, ct));

    [HttpGet("activity-feed")]
    public async Task<IActionResult> ActivityFeed(CancellationToken ct) =>
        Ok(await analyticsService.GetActivityFeedAsync(currentUser.CompanyId!, ct));

    [HttpGet("finance")]
    [RequireModule(ModuleName.Analytics)]
    public async Task<IActionResult> Finance(CancellationToken ct) =>
        Ok(await analyticsService.GetFinanceAsync(currentUser.CompanyId!, ct));

    [HttpGet("upcoming-meetings")]
    public async Task<IActionResult> UpcomingMeetings(CancellationToken ct) =>
        Ok(await analyticsService.GetUpcomingMeetingsAsync(currentUser.CompanyId!, currentUser.UserId!, ct));

    [HttpGet("forecast")]
    [RequireModule(ModuleName.Analytics)]
    public async Task<IActionResult> Forecast(CancellationToken ct) =>
        Ok(await analyticsService.GetForecastAsync(currentUser.CompanyId!, ct));
}
