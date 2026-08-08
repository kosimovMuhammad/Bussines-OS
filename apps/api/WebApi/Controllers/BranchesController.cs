using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Organization;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("branches")]
[Authorize]
[RequireModule(ModuleName.Organization)]
public class BranchesController(
    IOrganizationService organizationService,
    ICurrentUserContext currentUser,
    IValidator<CreateBranchRequest> createValidator,
    IValidator<UpdateBranchRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await organizationService.ListBranchesAsync(currentUser.CompanyId!, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await organizationService.GetBranchAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateBranchRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await organizationService.CreateBranchAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateBranchRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await organizationService.UpdateBranchAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await organizationService.RemoveBranchAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }
}
