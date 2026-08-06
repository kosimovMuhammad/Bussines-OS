using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Contacts;
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
    ICurrentUserContext currentUser,
    IValidator<CreateContactRequest> createValidator,
    IValidator<UpdateContactRequest> updateValidator) : ControllerBase
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
}
