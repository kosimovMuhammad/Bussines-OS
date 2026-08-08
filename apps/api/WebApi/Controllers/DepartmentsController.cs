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
[Route("departments")]
[Authorize]
[RequireModule(ModuleName.Organization)]
public class DepartmentsController(
    IOrganizationService organizationService,
    ICurrentUserContext currentUser,
    IValidator<CreateDepartmentRequest> createValidator,
    IValidator<UpdateDepartmentRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool flat, CancellationToken ct) =>
        flat
            ? Ok(await organizationService.ListDepartmentsFlatAsync(currentUser.CompanyId!, ct))
            : Ok(await organizationService.ListDepartmentsTreeAsync(currentUser.CompanyId!, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await organizationService.GetDepartmentAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateDepartmentRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await organizationService.CreateDepartmentAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateDepartmentRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await organizationService.UpdateDepartmentAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await organizationService.RemoveDepartmentAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }
}
