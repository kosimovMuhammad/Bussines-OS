using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Activity;
using BusinessOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

// Plural "/companies/:id" (distinct from the existing singular "/company", which always
// operates implicitly on the caller's own CompanyId) - added only for the Phase 6 timeline
// route the spec asks for by that exact shape. Since this system has no cross-tenant access
// at all, :id is only ever meaningful when it equals the caller's own company - anything
// else 403s rather than silently redirecting to "your own company's timeline instead".
[ApiController]
[Route("companies")]
[Authorize]
[RequireModule(ModuleName.Company)]
public class CompaniesController(IActivityLogService activityLogService, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> Timeline(string id, [FromQuery] QueryActivityLogRequest query, CancellationToken ct)
    {
        if (id != currentUser.CompanyId)
        {
            throw new ForbiddenException("Cannot view another company's timeline");
        }

        return Ok(await activityLogService.ListAsync(currentUser.CompanyId!, query, ct));
    }
}
