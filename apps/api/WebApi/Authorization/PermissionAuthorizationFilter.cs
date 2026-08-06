using BusinessOS.Application.Constants;
using BusinessOS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BusinessOS.WebApi.Authorization;

// Replaces the original PermissionsGuard - but fails CLOSED instead of open: an action
// with no [RequireModule] is denied, not silently allowed. See the phase-1 plan's
// Architecture section for why this is a deliberate behavior change, not a straight port.
public class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return Task.CompletedTask;
        }

        var moduleAttribute = GetAttribute<RequireModuleAttribute>(context);
        if (moduleAttribute is null)
        {
            context.Result = Forbid("Route has no declared module permission");
            return Task.CompletedTask;
        }

        var roleClaim = context.HttpContext.User.FindFirst("role")?.Value;
        if (roleClaim is null || !Enum.TryParse<Role>(roleClaim, out var role))
        {
            context.Result = Forbid("No role claim on the current user");
            return Task.CompletedTask;
        }

        var actionAttribute = GetAttribute<RequireActionAttribute>(context);
        var action = actionAttribute?.Action ?? InferActionFromHttpMethod(context.HttpContext.Request.Method);

        if (!RolePermissions.HasPermission(role, moduleAttribute.Module, action))
        {
            context.Result = Forbid("Role does not have this permission");
        }

        return Task.CompletedTask;
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
