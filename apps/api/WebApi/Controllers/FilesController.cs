using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Files;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("files")]
[Authorize]
[RequireModule(ModuleName.Files)]
public class FilesController(
    IFilesService filesService,
    ICurrentUserContext currentUser,
    IValidator<PresignUploadRequest> presignValidator,
    IValidator<ConfirmFileRequest> confirmValidator,
    IValidator<RestoreFileVersionRequest> restoreVersionValidator) : ControllerBase
{
    [HttpPost("presign")]
    public async Task<IActionResult> Presign(PresignUploadRequest request, CancellationToken ct)
    {
        await presignValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await filesService.PresignAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Confirm(ConfirmFileRequest request, CancellationToken ct)
    {
        await confirmValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await filesService.ConfirmAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryFilesRequest query, CancellationToken ct) =>
        Ok(await filesService.ListAsync(currentUser.CompanyId!, currentUser.UserId!, currentUser.Role!.Value, query, ct));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await filesService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpGet("{id}/versions")]
    public async Task<IActionResult> ListVersions(string id, CancellationToken ct) =>
        Ok(await filesService.ListVersionsAsync(currentUser.CompanyId!, id, ct));

    [HttpPost("{id}/restore-version")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> RestoreVersion(string id, RestoreFileVersionRequest request, CancellationToken ct)
    {
        await restoreVersionValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await filesService.RestoreVersionAsync(currentUser.CompanyId!, id, request.VersionId, currentUser.UserId!, ct));
    }
}
