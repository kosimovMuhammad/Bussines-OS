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
[Route("permissions")]
[Authorize]
[RequireModule(ModuleName.Rbac)]
public class PermissionsController(
    IPermissionsService permissionsService,
    ICurrentUserContext currentUser,
    IValidator<CreatePermissionRequest> createValidator,
    IValidator<UpdatePermissionRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? module, CancellationToken ct) =>
        Ok(await permissionsService.ListAsync(module, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await permissionsService.GetAsync(id, ct));

    // Mutations: platform-admin only, not tenant-facing - the permission catalog is a
    // global, shared-across-tenants list (see Permission.cs). Coarse gate is a Read
    // passthrough (Rbac:Read is granted to every role); the real check is
    // PermissionsService.EnsurePlatformAdminAsync, same pattern as PlansController.
    [HttpPost]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Create(CreatePermissionRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await permissionsService.CreateAsync(currentUser.UserId!, request, ct));
    }

    [HttpPatch("{id}")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Update(string id, UpdatePermissionRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await permissionsService.UpdateAsync(currentUser.UserId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await permissionsService.RemoveAsync(currentUser.UserId!, id, ct);
        return NoContent();
    }
}
