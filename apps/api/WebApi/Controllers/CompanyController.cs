using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Company;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("company")]
[Authorize]
[RequireModule(ModuleName.Company)]
public class CompanyController(
    ICompanyService companyService,
    ICurrentUserContext currentUser,
    IValidator<UpdateCompanyRequest> updateCompanyValidator,
    IValidator<UpdatePlanRequest> updatePlanValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await companyService.GetAsync(currentUser.CompanyId!, ct));

    [HttpPatch]
    [Authorize]
    public async Task<IActionResult> Update(UpdateCompanyRequest request, CancellationToken ct)
    {
        await updateCompanyValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await companyService.UpdateAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpGet("plan")]
    public async Task<IActionResult> GetPlan(CancellationToken ct)
    {
        var company = await companyService.GetAsync(currentUser.CompanyId!, ct);
        return Ok(new { plan = company.Plan });
    }

    [HttpPatch("plan")]
    [Authorize]
    public async Task<IActionResult> UpdatePlan(UpdatePlanRequest request, CancellationToken ct)
    {
        await updatePlanValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await companyService.UpdatePlanAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpGet("integrations")]
    public async Task<IActionResult> GetIntegrations(CancellationToken ct) =>
        Ok(await companyService.GetIntegrationsAsync(currentUser.CompanyId!, ct));
}
