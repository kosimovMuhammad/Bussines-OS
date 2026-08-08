using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Settings;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

// Read is granted to every role by default (RoleSeedingService's read-only-by-default
// convention); PATCH infers PermissionAction.Update from the HTTP method, which only
// Owner/Admin get by default - same Owner/Admin-only-for-mutations shape as Audit, without
// needing an explicit [RequireAction] on every PATCH action here.
[ApiController]
[Route("settings")]
[Authorize]
[RequireModule(ModuleName.Settings)]
public class SettingsController(
    ISettingsService settingsService,
    ICurrentUserContext currentUser,
    IValidator<UpdateAllSettingsRequest> updateAllValidator,
    IValidator<UpdateCompanySettingsRequest> updateCompanyValidator,
    IValidator<UpdateSecuritySettingsRequest> updateSecurityValidator,
    IValidator<UpdateBrandingSettingsRequest> updateBrandingValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await settingsService.GetAllAsync(currentUser.CompanyId!, ct));

    [HttpPatch]
    public async Task<IActionResult> UpdateAll(UpdateAllSettingsRequest request, CancellationToken ct)
    {
        await updateAllValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await settingsService.UpdateAllAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet("company")]
    public async Task<IActionResult> GetCompany(CancellationToken ct) =>
        Ok(await settingsService.GetCompanyAsync(currentUser.CompanyId!, ct));

    [HttpPatch("company")]
    public async Task<IActionResult> UpdateCompany(UpdateCompanySettingsRequest request, CancellationToken ct)
    {
        await updateCompanyValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await settingsService.UpdateCompanyAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet("security")]
    public async Task<IActionResult> GetSecurity(CancellationToken ct) =>
        Ok(await settingsService.GetSecurityAsync(currentUser.CompanyId!, ct));

    [HttpPatch("security")]
    public async Task<IActionResult> UpdateSecurity(UpdateSecuritySettingsRequest request, CancellationToken ct)
    {
        await updateSecurityValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await settingsService.UpdateSecurityAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding(CancellationToken ct) =>
        Ok(await settingsService.GetBrandingAsync(currentUser.CompanyId!, ct));

    [HttpPatch("branding")]
    public async Task<IActionResult> UpdateBranding(UpdateBrandingSettingsRequest request, CancellationToken ct)
    {
        await updateBrandingValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await settingsService.UpdateBrandingAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }
}
