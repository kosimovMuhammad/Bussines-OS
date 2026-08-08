using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.WebApi.Authorization;

// Database-backed replacement for the old RolePermissions.cs hardcoded matrix - reads the
// caller's RbacRole grant plus any per-user UserPermissionOverride from the DB on every
// request. Still fails CLOSED: an action with no [RequireModule] is denied, not silently
// allowed, and a user with no RbacRoleId (shouldn't happen post Phase 1, but defensively)
// is denied rather than falling back to some default.
public class PermissionAuthorizationFilter(IAppDbContext db) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var moduleAttribute = GetAttribute<RequireModuleAttribute>(context);
        if (moduleAttribute is null)
        {
            context.Result = Forbid("Route has no declared module permission");
            return;
        }

        var userId = context.HttpContext.User.FindFirst("sub")?.Value;
        if (userId is null)
        {
            context.Result = Forbid("No user id claim on the current token");
            return;
        }

        var actionAttribute = GetAttribute<RequireActionAttribute>(context);
        var action = actionAttribute?.Action ?? InferActionFromHttpMethod(context.HttpContext.Request.Method);
        var permissionKey = $"{moduleAttribute.Module.ToString().ToLowerInvariant()}.{action.ToString().ToLowerInvariant()}";

        if (!await HasPermissionAsync(userId, permissionKey))
        {
            context.Result = Forbid("Role does not have this permission");
        }
    }

    private async Task<bool> HasPermissionAsync(string userId, string permissionKey)
    {
        var rbacRoleId = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.RbacRoleId)
            .SingleOrDefaultAsync();

        // A per-user override always wins, in either direction: it can grant a permission
        // the role doesn't have, or revoke one the role does have.
        var overrideAllow = await db.UserPermissionOverrides.AsNoTracking()
            .Where(o => o.UserId == userId && o.Permission.Key == permissionKey)
            .Select(o => (bool?)o.Allow)
            .SingleOrDefaultAsync();

        if (overrideAllow.HasValue)
        {
            return overrideAllow.Value;
        }

        if (rbacRoleId is null)
        {
            return false;
        }

        return await db.RolePermissions.AsNoTracking()
            .AnyAsync(rp => rp.RoleId == rbacRoleId && rp.Permission.Key == permissionKey);
    }

    private static T? GetAttribute<T>(AuthorizationFilterContext context) where T : Attribute
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return null;
        }

        return descriptor.MethodInfo.GetCustomAttributes(typeof(T), inherit: true).FirstOrDefault() as T
            ?? descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(T), inherit: true).FirstOrDefault() as T;
    }

    private static PermissionAction InferActionFromHttpMethod(string method) => method.ToUpperInvariant() switch
    {
        "GET" => PermissionAction.Read,
        "POST" => PermissionAction.Create,
        "PUT" or "PATCH" => PermissionAction.Update,
        "DELETE" => PermissionAction.Delete,
        _ => PermissionAction.Read,
    };

    private static ObjectResult Forbid(string message) => new(new { statusCode = 403, message, error = "Forbidden" })
    {
        StatusCode = 403,
    };
}
