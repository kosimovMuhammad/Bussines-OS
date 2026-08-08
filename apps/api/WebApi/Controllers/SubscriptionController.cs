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

// Thin tenant-facing wrapper over the existing Billing services (ISubscriptionService,
// ISubscriptionPlanService, ILimitEnforcementService), which already power
// /company/subscription, /plans and /company/usage - this just exposes the exact
// /subscription/* route shape the Phase 3 spec asks for, without duplicating the
// underlying business logic. Upgrade/Cancel require billing.update (Owner/Admin by
// default); GET routes only need billing.read, granted to every role by default.
[ApiController]
[Route("subscription")]
[Authorize]
[RequireModule(ModuleName.Billing)]
public class SubscriptionController(
    ISubscriptionService subscriptionService,
    ISubscriptionPlanService planService,
    ILimitEnforcementService limitEnforcement,
    ICurrentUserContext currentUser,
    IValidator<RequestUpgradeRequest> upgradeValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCurrent(CancellationToken ct) =>
        Ok(await subscriptionService.GetCurrentAsync(currentUser.CompanyId!, ct));

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await planService.ListAsync(includeInactive, ct));

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(CancellationToken ct) =>
        Ok(await limitEnforcement.GetUsageAsync(currentUser.CompanyId!, ct));

    [HttpPost("upgrade")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Upgrade(RequestUpgradeRequest request, CancellationToken ct)
    {
        await upgradeValidator.ValidateAndThrowAppAsync(request, ct);
        var result = await subscriptionService.RequestUpgradeAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct);
        return Accepted(result);
    }

    [HttpPost("cancel")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Cancel([FromBody] CancelSubscriptionRequest? request, CancellationToken ct) =>
        Ok(await subscriptionService.CancelAsync(currentUser.CompanyId!, currentUser.UserId!, request ?? new CancelSubscriptionRequest(), ct));
}
