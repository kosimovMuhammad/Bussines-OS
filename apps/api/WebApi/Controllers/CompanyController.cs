using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Billing;
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
    ISubscriptionService subscriptionService,
    ILimitEnforcementService limitEnforcement,
    IFeatureAccessService featureAccess,
    ISubscriptionInvoiceService invoiceService,
    ICurrentUserContext currentUser,
    IValidator<UpdateCompanyRequest> updateCompanyValidator,
    IValidator<UpdatePlanRequest> updatePlanValidator,
    IValidator<TransferOwnershipRequest> transferOwnershipValidator,
    IValidator<ChangePlanRequest> changePlanValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await companyService.GetAsync(currentUser.CompanyId!, ct));

    [HttpPatch]
    [Authorize]
    public async Task<IActionResult> Update(UpdateCompanyRequest request, CancellationToken ct)
    {
        await updateCompanyValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await companyService.UpdateAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
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
        return Ok(await companyService.UpdatePlanAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet("integrations")]
    public async Task<IActionResult> GetIntegrations(CancellationToken ct) =>
        Ok(await companyService.GetIntegrationsAsync(currentUser.CompanyId!, ct));

    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription(CancellationToken ct) =>
        Ok(await subscriptionService.GetCurrentAsync(currentUser.CompanyId!, ct));

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(CancellationToken ct) =>
        Ok(await limitEnforcement.GetUsageAsync(currentUser.CompanyId!, ct));

    [HttpGet("features")]
    public async Task<IActionResult> GetFeatures(CancellationToken ct) =>
        Ok(await featureAccess.GetFeatureFlagsAsync(currentUser.CompanyId!, ct));

    [HttpPost("subscription/change-plan")]
    [Authorize]
    public async Task<IActionResult> ChangePlan(ChangePlanRequest request, CancellationToken ct)
    {
        await changePlanValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await subscriptionService.ChangePlanAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpPost("subscription/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelSubscription(CancelSubscriptionRequest request, CancellationToken ct) =>
        Ok(await subscriptionService.CancelAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] QuerySubscriptionInvoicesRequest query, CancellationToken ct) =>
        Ok(await invoiceService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("invoices/{id}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(string id, CancellationToken ct)
    {
        var pdfBytes = await invoiceService.RenderPdfAsync(currentUser.CompanyId!, id, ct);
        return File(pdfBytes, "application/pdf", $"invoice-{id}.pdf");
    }

    [HttpPost("transfer-ownership")]
    [Authorize]
    public async Task<IActionResult> TransferOwnership(TransferOwnershipRequest request, CancellationToken ct)
    {
        await transferOwnershipValidator.ValidateAndThrowAppAsync(request, ct);
        await companyService.TransferOwnershipAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct);
        return NoContent();
    }
}
