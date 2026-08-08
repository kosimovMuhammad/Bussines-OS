using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("org-chart")]
[Authorize]
[RequireModule(ModuleName.Organization)]
public class OrgChartController(IOrganizationService organizationService, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await organizationService.GetOrgChartAsync(currentUser.CompanyId!, ct));

    [HttpGet("user/{id}")]
    public async Task<IActionResult> GetUserChain(string id, CancellationToken ct) =>
        Ok(await organizationService.GetUserOrgChainAsync(currentUser.CompanyId!, id, ct));
}
