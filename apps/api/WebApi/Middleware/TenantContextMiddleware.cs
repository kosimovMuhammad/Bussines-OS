using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Tenancy;

namespace BusinessOS.WebApi.Middleware;

// Equivalent of the old TenantGuard: right after JWT auth succeeds, publish the
// caller's companyId/userId/role into the AsyncLocal-backed tenant/user context
// that EF Core's global query filters, SaveChanges interceptor, and services read.
public class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenantContext tenantContext, CurrentUserContext userContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst("sub")?.Value;
            var companyId = context.User.FindFirst("companyId")?.Value;
            var roleClaim = context.User.FindFirst("role")?.Value;

            if (userId is null || companyId is null || roleClaim is null || !Enum.TryParse<Role>(roleClaim, out var role))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { statusCode = 403, message = "Company муайян нашуд", error = "Forbidden" });
                return;
            }

            tenantContext.Set(companyId);
            userContext.Set(userId, companyId, role);
        }

        await next(context);
    }
}
