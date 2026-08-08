using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Inventory;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("inventory")]
[Authorize]
[RequireModule(ModuleName.Inventory)]
public class InventoryController(
    IInventoryService inventoryService,
    ICurrentUserContext currentUser,
    IValidator<CreateInventoryRequest> createValidator,
    IValidator<UpdateInventoryRequest> updateValidator,
    IValidator<StockMovementRequest> stockMovementValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryInventoryRequest query, CancellationToken ct) =>
        Ok(await inventoryService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await inventoryService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateInventoryRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await inventoryService.CreateAsync(currentUser.CompanyId!, currentUser.UserId, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateInventoryRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await inventoryService.UpdateAsync(currentUser.CompanyId!, currentUser.UserId, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await inventoryService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPost("stock-in")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> StockIn(StockMovementRequest request, CancellationToken ct)
    {
        await stockMovementValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await inventoryService.StockInAsync(currentUser.CompanyId!, currentUser.UserId, request, ct));
    }

    [HttpPost("stock-out")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> StockOut(StockMovementRequest request, CancellationToken ct)
    {
        await stockMovementValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await inventoryService.StockOutAsync(currentUser.CompanyId!, currentUser.UserId, request, ct));
    }
}
