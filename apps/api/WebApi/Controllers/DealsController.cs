using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Deals;
using BusinessOS.Domain.Entities;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("deals")]
[Authorize]
[RequireModule(ModuleName.Deals)]
public class DealsController(
    IDealsService dealsService,
    IArchiveService archiveService,
    ICurrentUserContext currentUser,
    IValidator<CreateDealRequest> createValidator,
    IValidator<UpdateDealRequest> updateValidator,
    IValidator<UpdateDealStageRequest> updateStageValidator,
    IValidator<BulkUpdateDealsRequest> bulkUpdateValidator,
    IValidator<BulkDeleteRequest> bulkDeleteValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryDealsRequest query, CancellationToken ct) =>
        Ok(await dealsService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await dealsService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> Timeline(string id, CancellationToken ct) =>
        Ok(await dealsService.GetTimelineAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateDealRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await dealsService.CreateAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateDealRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await dealsService.UpdateAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpPatch("{id}/stage")]
    public async Task<IActionResult> UpdateStage(string id, UpdateDealStageRequest request, CancellationToken ct)
    {
        await updateStageValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await dealsService.UpdateStageAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await dealsService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPatch("{id}/archive")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Archive(string id, CancellationToken ct)
    {
        await archiveService.ArchiveAsync<Deal>(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPost("{id}/restore")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Restore(string id, CancellationToken ct)
    {
        await archiveService.RestoreAsync<Deal>(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPost("bulk-update")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> BulkUpdate(BulkUpdateDealsRequest request, CancellationToken ct)
    {
        await bulkUpdateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await dealsService.BulkUpdateAsync(currentUser.CompanyId!, request.Ids, request.Patch, ct));
    }

    [HttpPost("bulk-delete")]
    [RequireAction(PermissionAction.Delete)]
    public async Task<IActionResult> BulkDelete(BulkDeleteRequest request, CancellationToken ct)
    {
        await bulkDeleteValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await dealsService.BulkDeleteAsync(currentUser.CompanyId!, request.Ids, ct));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var csv = await dealsService.ExportCsvAsync(currentUser.CompanyId!, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"deals-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("import")]
    [RequireAction(PermissionAction.Create)]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var csvContent = await reader.ReadToEndAsync(ct);
        return Ok(await dealsService.ImportCsvAsync(currentUser.CompanyId!, csvContent, ct));
    }
}
