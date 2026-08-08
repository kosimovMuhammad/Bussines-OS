using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.ApiKeys;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("api-keys")]
[Authorize]
[RequireModule(ModuleName.ApiKeys)]
public class ApiKeysController(
    IApiKeysService apiKeysService,
    ICurrentUserContext currentUser,
    IValidator<CreateApiKeyRequest> createValidator,
    IValidator<UpdateApiKeyRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await apiKeysService.ListAsync(currentUser.CompanyId!, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateApiKeyRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await apiKeysService.CreateAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateApiKeyRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await apiKeysService.UpdateAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await apiKeysService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }
}
