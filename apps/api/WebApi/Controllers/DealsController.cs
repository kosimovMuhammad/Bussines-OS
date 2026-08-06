using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Deals;
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
    ICurrentUserContext currentUser,
    IValidator<CreateDealRequest> createValidator,
    IValidator<UpdateDealRequest> updateValidator,
    IValidator<UpdateDealStageRequest> updateStageValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryDealsRequest query, CancellationToken ct) =>
        Ok(await dealsService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await dealsService.GetAsync(currentUser.CompanyId!, id, ct));

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
}
