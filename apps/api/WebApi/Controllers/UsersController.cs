using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Audit;
using BusinessOS.Application.ViewModels.Organization;
using BusinessOS.Application.ViewModels.Rbac;
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
    IAuditLogService auditLogService,
    IOrganizationService organizationService,
    IPermissionsService permissionsService,
    ICurrentUserContext currentUser,
    IValidator<UpdateUserRequest> updateUserValidator,
    IValidator<InviteUserRequest> inviteUserValidator,
    IValidator<UpdateRoleRequest> updateRoleValidator,
    IValidator<SetPasswordRequest> setPasswordValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    IValidator<UpdateAssignmentRequest> updateAssignmentValidator,
    IValidator<UpdateUserPermissionsRequest> updateUserPermissionsValidator,
    IValidator<BulkInviteUsersRequest> bulkInviteValidator) : ControllerBase
{
    [HttpGet("me")]
    [RequireModule(ModuleName.Profile)]
    public async Task<IActionResult> GetMe(CancellationToken ct) =>
        Ok(await usersService.GetMeAsync(currentUser.UserId!, ct));

    // RequireAction(Read) override: updating your own name/avatar must be available to every
    // authenticated role, not just the roles the default RBAC seeding grants Update to on the
    // Profile module. Same pattern as the password endpoints below and AuthController's logout-all.
    [HttpPatch("me")]
    [RequireModule(ModuleName.Profile)]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> UpdateMe(UpdateUserRequest request, CancellationToken ct)
    {
        await updateUserValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await usersService.UpdateMeAsync(currentUser.UserId!, request, ct));
    }

    // RequireAction(Read) override: setting/changing your own password must be available to
    // every authenticated role, not just the roles the default RBAC seeding grants Create to
    // on the Profile module. Same pattern as AuthController's logout-all.
    [HttpPost("me/set-password")]
    [RequireModule(ModuleName.Profile)]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request, CancellationToken ct)
    {
        await setPasswordValidator.ValidateAndThrowAppAsync(request, ct);
        await usersService.SetPasswordAsync(currentUser.UserId!, request, ct);
        return Ok(new { success = true });
    }

    [HttpPost("me/change-password")]
    [RequireModule(ModuleName.Profile)]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await changePasswordValidator.ValidateAndThrowAppAsync(request, ct);
        await usersService.ChangePasswordAsync(currentUser.UserId!, request, ct);
        return Ok(new { success = true });
    }

    [HttpGet("me/login-history")]
    [RequireModule(ModuleName.Profile)]
    public async Task<IActionResult> GetMyLoginHistory([FromQuery] QueryLoginHistoryRequest query, CancellationToken ct) =>
        Ok(await usersService.GetMyLoginHistoryAsync(currentUser.UserId!, query, ct));

    [HttpGet("me/security-history")]
    [RequireModule(ModuleName.Profile)]
    public async Task<IActionResult> GetMySecurityHistory([FromQuery] QueryAuditLogRequest query, CancellationToken ct) =>
        Ok(await auditLogService.GetSecurityHistoryAsync(currentUser.CompanyId!, currentUser.UserId!, query, ct));

    [HttpGet]
    [RequireModule(ModuleName.Users)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await usersService.ListAsync(currentUser.CompanyId!, ct));

    [HttpGet("{id}")]
    [RequireModule(ModuleName.Users)]
    public async Task<IActionResult> GetOne(string id, CancellationToken ct) =>
        Ok(await usersService.GetOneAsync(currentUser.CompanyId!, id, ct));

    [HttpGet("{id}/login-history")]
    [RequireModule(ModuleName.Users)]
    public async Task<IActionResult> GetLoginHistory(string id, [FromQuery] QueryLoginHistoryRequest query, CancellationToken ct) =>
        Ok(await usersService.GetLoginHistoryAsync(currentUser.CompanyId!, currentUser.Role!.Value, id, query, ct));

    [HttpPost("invite")]
    [RequireModule(ModuleName.Users)]
    [Authorize]
    public async Task<IActionResult> Invite(InviteUserRequest request, CancellationToken ct)
    {
        await inviteUserValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await usersService.InviteAsync(currentUser.CompanyId!, currentUser.UserId!, request, ct));
    }

    [HttpPost("bulk-invite")]
    [RequireModule(ModuleName.Users)]
    [Authorize]
    public async Task<IActionResult> BulkInvite(BulkInviteUsersRequest request, CancellationToken ct)
    {
        await bulkInviteValidator.ValidateAndThrowAppAsync(request, ct);
        return Ok(await usersService.BulkInviteAsync(currentUser.CompanyId!, currentUser.UserId!, request.Users, ct));
    }

    [HttpGet("export")]
    [RequireModule(ModuleName.Users)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var csv = await usersService.ExportCsvAsync(currentUser.CompanyId!, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"users-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("import")]
    [RequireModule(ModuleName.Users)]
    [Authorize]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var csvContent = await reader.ReadToEndAsync(ct);
        return Ok(await usersService.ImportCsvAsync(currentUser.CompanyId!, currentUser.UserId!, csvContent, ct));
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
        return Ok(await usersService.UpdateAsync(currentUser.CompanyId!, currentUser.UserId!, currentUser.Role!.Value, id, request, ct));
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

    [HttpGet("{id}/permissions")]
    [RequireModule(ModuleName.Users)]
    public async Task<IActionResult> GetPermissions(string id, CancellationToken ct) =>
        Ok(await permissionsService.GetEffectivePermissionsAsync(currentUser.CompanyId!, id, ct));

    [HttpPatch("{id}/permissions")]
    [RequireModule(ModuleName.Users)]
    public async Task<IActionResult> UpdatePermissions(string id, UpdateUserPermissionsRequest request, CancellationToken ct)
    {
        await updateUserPermissionsValidator.ValidateAndThrowAppAsync(request, ct);
        await permissionsService.UpdateUserPermissionOverridesAsync(currentUser.CompanyId!, currentUser.UserId!, id, request, ct);
        return NoContent();
    }

    // Deliberately gated on Read, not Update: the coarse role matrix can't express
    // "Owner/Admin, or this specific user's department manager" - real authorization
    // happens inside OrganizationService.UpdateAssignmentAsync. See the Company
    // Structure Phase 1 plan's "necessary wrinkle" note for why.
    [HttpPatch("{id}/assignment")]
    [RequireModule(ModuleName.Users)]
    [RequireAction(PermissionAction.Read)]
    public async Task<IActionResult> UpdateAssignment(string id, UpdateAssignmentRequest request, CancellationToken ct)
    {
        await updateAssignmentValidator.ValidateAndThrowAppAsync(request, ct);
        await organizationService.UpdateAssignmentAsync(currentUser.CompanyId!, currentUser.UserId!, currentUser.Role!.Value, id, request, ct);
        return NoContent();
    }
}
