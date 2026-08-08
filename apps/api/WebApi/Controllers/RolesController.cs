using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Rbac;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("roles")]
[Authorize]
[RequireModule(ModuleName.Rbac)]
public class RolesController(
    IRolesService rolesService,
    ICurrentUserContext currentUser,
    IValidator<CreateRoleRequest> createValidator,
    IValidator<UpdateRoleDetailsRequest> updateValidator,
    IValidator<AttachPermissionRequest> attachValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await rolesService.ListAsync(currentUser.CompanyId!, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await rolesService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateRoleRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await rolesService.CreateAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateRoleDetailsRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await rolesService.UpdateAsync(currentUser.CompanyId!, currentUser.UserId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await rolesService.RemoveAsync(currentUser.CompanyId!, currentUser.UserId!, id, ct);
        return NoContent();
    }

    [HttpPost("{roleId}/permissions")]
    public async Task<IActionResult> AttachPermission(string roleId, AttachPermissionRequest request, CancellationToken ct)
    {
        await attachValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await rolesService.AttachPermissionAsync(currentUser.CompanyId!, currentUser.UserId!, roleId, request.PermissionId, ct));
    }

    [HttpDelete("{roleId}/permissions/{permissionId}")]
    public async Task<IActionResult> DetachPermission(string roleId, string permissionId, CancellationToken ct)
    {
        await rolesService.DetachPermissionAsync(currentUser.CompanyId!, currentUser.UserId!, roleId, permissionId, ct);
        return NoContent();
    }
}
