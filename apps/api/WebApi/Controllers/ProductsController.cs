using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Inventory;
using BusinessOS.Domain.Entities;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("products")]
[Authorize]
[RequireModule(ModuleName.Products)]
public class ProductsController(
    IProductsService productsService,
    IArchiveService archiveService,
    ICurrentUserContext currentUser,
    IValidator<CreateProductRequest> createValidator,
    IValidator<UpdateProductRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryProductsRequest query, CancellationToken ct) =>
        Ok(await productsService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await productsService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await productsService.CreateAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateProductRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await productsService.UpdateAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await productsService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPatch("{id}/archive")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Archive(string id, CancellationToken ct)
    {
        await archiveService.ArchiveAsync<Product>(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPost("{id}/restore")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Restore(string id, CancellationToken ct)
    {
        await archiveService.RestoreAsync<Product>(currentUser.CompanyId!, id, ct);
        return NoContent();
    }
}
