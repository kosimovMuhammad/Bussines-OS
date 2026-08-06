using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.WebApi.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController(
    IUsersService usersService,
    ICurrentUserContext currentUser,
    IValidator<UpdateUserRequest> updateUserValidator,
    IValidator<InviteUserRequest> inviteUserValidator,
    IValidator<UpdateRoleRequest> updateRoleValidator) : ControllerBase
{
    [HttpGet("me")]
    [RequireModule(ModuleName.Profile)]
    public async Task<IActionResult> GetMe(CancellationToken ct) =>
        Ok(await usersService.GetMeAsync(currentUser.UserId!, ct));

    [HttpPatch("me")]
    [RequireModule(ModuleName.Profile)]
    public async Task<IActionResult> UpdateMe(UpdateUserRequest request, CancellationToken ct)
    {
        await updateUserValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await usersService.UpdateMeAsync(currentUser.UserId!, request, ct));
    }

    [HttpGet]
    [RequireModule(ModuleName.Users)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await usersService.ListAsync(currentUser.CompanyId!, ct));

    [HttpGet("{id}")]
    [RequireModule(ModuleName.Users)]
    public async Task<IActionResult> GetOne(string id, CancellationToken ct) =>
        Ok(await usersService.GetOneAsync(currentUser.CompanyId!, id, ct));

    [HttpPost("invite")]
    [RequireModule(ModuleName.Users)]
    [Authorize]
    public async Task<IActionResult> Invite(InviteUserRequest request, CancellationToken ct)
    {
        await inviteUserValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await usersService.InviteAsync(currentUser.CompanyId!, request, ct));
    }

    [HttpPost("{id}/resend-invite")]
    [RequireModule(ModuleName.Users)]
    [Authorize]
    public async Task<IActionResult> ResendInvite(string id, CancellationToken ct) =>
        Ok(await usersService.ResendInviteAsync(currentUser.CompanyId!, id, ct));

    [HttpPatch("{id}")]
    [RequireModule(ModuleName.Users)]
    [Authorize]
    public async Task<IActionResult> Update(string id, UpdateUserRequest request, CancellationToken ct)
    {
        await updateUserValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await usersService.UpdateAsync(currentUser.CompanyId!, currentUser.Role!.Value, id, request, ct));
    }

    [HttpPatch("{id}/role")]
    [RequireModule(ModuleName.Users)]
    [Authorize]
    public async Task<IActionResult> UpdateRole(string id, UpdateRoleRequest request, CancellationToken ct)
    {
        await updateRoleValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await usersService.UpdateRoleAsync(currentUser.CompanyId!, currentUser.UserId!, currentUser.Role!.Value, id, request, ct));
    }

    [HttpDelete("{id}")]
    [RequireModule(ModuleName.Users)]
    [Authorize]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        await usersService.RemoveAsync(currentUser.CompanyId!, currentUser.UserId!, id, ct);
        return NoContent();
    }
}
