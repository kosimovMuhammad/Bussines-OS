using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Finance;
using BusinessOS.Domain.Entities;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("contracts")]
[Authorize]
[RequireModule(ModuleName.Contracts)]
public class ContractsController(
    IContractsService contractsService,
    IArchiveService archiveService,
    ICurrentUserContext currentUser,
    IValidator<CreateContractRequest> createValidator,
    IValidator<UpdateContractRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryContractsRequest query, CancellationToken ct) =>
        Ok(await contractsService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await contractsService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateContractRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await contractsService.CreateAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateContractRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await contractsService.UpdateAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await contractsService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPost("{id}/sign")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Sign(string id, CancellationToken ct) =>
        Ok(await contractsService.SignAsync(currentUser.CompanyId!, currentUser.UserId!, id, GetClientIp(), ct));

    [HttpPatch("{id}/archive")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Archive(string id, CancellationToken ct)
    {
        await archiveService.ArchiveAsync<Contract>(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPost("{id}/restore")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Restore(string id, CancellationToken ct)
    {
        await archiveService.RestoreAsync<Contract>(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
