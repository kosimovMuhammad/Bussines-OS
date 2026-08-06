using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Communications;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("communications")]
[Authorize]
[RequireModule(ModuleName.Communications)]
public class CommunicationsController(
    ICommunicationsService communicationsService,
    ICurrentUserContext currentUser,
    IValidator<CreateNoteRequest> createNoteValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] QueryCommunicationsRequest query, CancellationToken ct) =>
        Ok(await communicationsService.ListAsync(currentUser.CompanyId!, query, ct));

    [HttpPost("notes")]
    public async Task<IActionResult> CreateNote(CreateNoteRequest request, CancellationToken ct)
    {
        await createNoteValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await communicationsService.CreateNoteAsync(currentUser.CompanyId!, request, ct));
    }
}
