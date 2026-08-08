using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Organization;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("teams")]
[Authorize]
[RequireModule(ModuleName.Organization)]
public class TeamsController(
    IOrganizationService organizationService,
    ICurrentUserContext currentUser,
    IValidator<CreateTeamRequest> createValidator,
    IValidator<UpdateTeamRequest> updateValidator,
    IValidator<AddTeamMemberRequest> addMemberValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await organizationService.ListTeamsAsync(currentUser.CompanyId!, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) =>
        Ok(await organizationService.GetTeamAsync(currentUser.CompanyId!, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateTeamRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await organizationService.CreateTeamAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateTeamRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await organizationService.UpdateTeamAsync(currentUser.CompanyId!, id, request, ct));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await organizationService.RemoveTeamAsync(currentUser.CompanyId!, id, ct);
        return NoContent();
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> ListMembers(string id, CancellationToken ct) =>
        Ok(await organizationService.ListTeamMembersAsync(currentUser.CompanyId!, id, ct));

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(string id, AddTeamMemberRequest request, CancellationToken ct)
    {
        await addMemberValidator.ValidateAndThrowAppAsync(request, ct);
        await organizationService.AddTeamMemberAsync(currentUser.CompanyId!, id, request, ct);
        return Ok();
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(string id, string userId, CancellationToken ct)
    {
        await organizationService.RemoveTeamMemberAsync(currentUser.CompanyId!, id, userId, ct);
        return NoContent();
    }
}
