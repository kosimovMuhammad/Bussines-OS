using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Contacts;
using BusinessOS.Domain.Entities;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("contacts")]
[Authorize]
[RequireModule(ModuleName.Contacts)]
public class ContactsController(
    IContactsService contactsService,
    IArchiveService archiveService,
    ICurrentUserContext currentUser,
    IValidator<CreateContactRequest> createValidator,
    IValidator<UpdateContactRequest> updateValidator,
    IValidator<BulkUpdateContactsRequest> bulkUpdateValidator,
    IValidator<BulkDeleteRequest> bulkDeleteValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryContactsRequest query, CancellationToken ct) =>
        Ok(await contactsService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await contactsService.GetAsync(currentUser.CompanyId!, id, ct));

    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> Timeline(string id, CancellationToken ct) =>
        Ok(await contactsService.GetTimelineAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateContactRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await contactsService.CreateAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, UpdateContactRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await contactsService.UpdateAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await contactsService.RemoveAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPatch("{id}/archive")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Archive(string id, CancellationToken ct)
    {
        await archiveService.ArchiveAsync<Contact>(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPost("{id}/restore")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> Restore(string id, CancellationToken ct)
    {
        await archiveService.RestoreAsync<Contact>(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpPost("bulk-update")]
    [RequireAction(PermissionAction.Update)]
    public async Task<IActionResult> BulkUpdate(BulkUpdateContactsRequest request, CancellationToken ct)
    {
        await bulkUpdateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await contactsService.BulkUpdateAsync(currentUser.CompanyId!, request.Ids, request.Patch, ct));
    }

    [HttpPost("bulk-delete")]
    [RequireAction(PermissionAction.Delete)]
    public async Task<IActionResult> BulkDelete(BulkDeleteRequest request, CancellationToken ct)
    {
        await bulkDeleteValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await contactsService.BulkDeleteAsync(currentUser.CompanyId!, request.Ids, ct));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var csv = await contactsService.ExportCsvAsync(currentUser.CompanyId!, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"contacts-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("import")]
    [RequireAction(PermissionAction.Create)]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var csvContent = await reader.ReadToEndAsync(ct);
        return Ok(await contactsService.ImportCsvAsync(currentUser.CompanyId!, csvContent, ct));
    }
}
