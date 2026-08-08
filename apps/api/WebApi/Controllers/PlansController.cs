using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Billing;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("plans")]
public class PlansController(
    ISubscriptionPlanService planService,
    ICurrentUserContext currentUser,
    IValidator<CreateSubscriptionPlanRequest> createValidator,
    IValidator<UpdateSubscriptionPlanRequest> updateValidator) : ControllerBase
{
    // Public - for the pricing page. No [RequireModule] needed since [AllowAnonymous]
    // routes are exempt from PermissionAuthorizationFilter's fail-closed gate.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await planService.ListAsync(includeInactive, ct));

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await planService.GetAsync(id, ct));

    // Mutations: platform-admin only, not tenant-facing. Coarse gate is a Read passthrough
    // (Billing module Read is granted to every role) - the real check is
    // SubscriptionPlanService.EnsurePlatformAdminAsync, same pattern as the Company
    // Structure module's delegated-assignment endpoint.
    [HttpPost]
    [Authorize]
    [RequireModule(ModuleName.Billing)]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Create(CreateSubscriptionPlanRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await planService.CreateAsync(currentUser.UserId!, request, ct));
    }

    [HttpPut("{id}")]
    [Authorize]
    [RequireModule(ModuleName.Billing)]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Update(string id, UpdateSubscriptionPlanRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await planService.UpdateAsync(currentUser.UserId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    [Authorize]
    [RequireModule(ModuleName.Billing)]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await planService.RemoveAsync(currentUser.UserId!, id, ct);
        return NoContent();
    }
}
