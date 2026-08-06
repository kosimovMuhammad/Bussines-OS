using BusinessOS.Application.Configurations.Exceptions;

namespace BusinessOS.WebApi.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, error, message) = ex switch
        {
            ValidationAppException v => (422, "Unprocessable Entity", (object)v.Errors),
            NotFoundException nf => (404, "Not Found", (object)nf.Message),
            ForbiddenException f => (403, "Forbidden", (object)f.Message),
            ConflictException c => (409, "Conflict", (object)c.Message),
            UnauthorizedAppException u => (401, "Unauthorized", (object)u.Message),
            _ => (500, "Internal Server Error", (object)"An unexpected error occurred"),
        };

        if (status == 500)
        {
            logger.LogError(ex, "Unhandled exception");
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { statusCode = status, message, error });
    }
}
