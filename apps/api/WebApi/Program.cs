using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BusinessOS.Application;
using BusinessOS.Infrastructure;
using BusinessOS.Infrastructure.Realtime;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Health;
using BusinessOS.WebApi.Logging;
using BusinessOS.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// The default Windows host also logs to the Event Log. That provider is disposed before the host
// finishes stopping, which produced ObjectDisposedException noise on every shutdown, and nobody reads
// the Event Log for this app. Console (+ Debug in an IDE) is all we need; production collects stdout.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
// Development only: also write logs to logs/dev-*.log (gitignored) so they can be read from disk.
var devLogPath = builder.AddDevFileLogging();

// Background jobs (token refresh, workflow scheduler, ...) are secondary to serving requests and
// webhooks. The default, StopHost, means a single faulted job takes the whole API down. Ignore
// keeps the API up; the job base classes already catch and log every tick failure, so reaching this
// safety net means a bug, which is logged as an error by the host.
builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

ValidateRequiredConfiguration(builder.Configuration);

var corsOrigins = BuildCorsOrigins(builder.Configuration, builder.Environment);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services
    .AddControllers(options => options.Filters.Add<PermissionAuthorizationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options => options.AddPolicy("default", policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Otherwise short claim names like "sub"/"role" get silently remapped to legacy
        // long-form URIs, and every FindFirst("sub")/FindFirst("role") lookup returns null.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT_ACCESS_SECRET"]!)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
        options.Events = new JwtBearerEvents
        {
            // Browsers can't set an Authorization header on the WebSocket handshake, so
            // SignalR's JS client sends the token as ?access_token=... instead - standard
            // SignalR+JWT pattern, scoped to only the hub path so every other endpoint still
            // requires a real Authorization header.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SupportNonNullableReferenceTypes();
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BusinessOS API", Version = "v1" });
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "bearer" } },
            []
        },
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "BusinessOS API v1");
    options.RoutePrefix = "docs";
});

if (app.Environment.IsDevelopment())
{
    // Serves files LocalDiskFileStorageService wrote to App_Data/uploads - the local stand-in
    // for a real S3 GET presigned URL. See DependencyInjection.AddInfrastructure.
    var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "App_Data", "uploads");
    Directory.CreateDirectory(uploadsRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRoot),
        RequestPath = "/uploads",
    });
}

app.UseCors("default");
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

// /health/live: process is up (no dependencies - use for restarts). /health: ready to serve, i.e. the
// database answers (use for uptime monitors / load balancers). Anonymous on purpose; no sensitive data.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = c => c.Tags.Contains("live"), ResponseWriter = HealthResponseWriter.WriteAsync })
    .AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") || c.Tags.Contains("live"), ResponseWriter = HealthResponseWriter.WriteAsync })
    .AllowAnonymous();

app.MapControllers();
app.MapHub<MessagingHub>("/hubs/messaging");

if (devLogPath is not null)
{
    app.Logger.LogInformation("Development file log: {LogPath}", devLogPath);
}

app.Run();

static void ValidateRequiredConfiguration(IConfiguration configuration)
{
    var errors = new List<string>();

    void RequireMinLength(string key, int minLength)
    {
        var value = configuration[key];
        if (string.IsNullOrEmpty(value))
        {
            errors.Add($"{key} is required");
        }
        else if (value.Length < minLength)
        {
            errors.Add($"{key} must be at least {minLength} characters");
        }
    }

    if (string.IsNullOrEmpty(configuration.GetConnectionString("DefaultConnection")))
    {
        errors.Add("ConnectionStrings:DefaultConnection is required");
    }

    RequireMinLength("JWT_ACCESS_SECRET", 32);
    RequireMinLength("JWT_REFRESH_SECRET", 32);
    RequireMinLength("ENCRYPTION_KEY", 32);

    // Gmail OAuth previously failed at redirect time with Google's cryptic "Missing required
    // parameter: client_id" instead of failing here at startup - GmailClient.GetAuthUrl reads
    // these same flat keys straight from IConfiguration with no null-check of its own.
    if (string.IsNullOrEmpty(configuration["GOOGLE_CLIENT_ID"]))
    {
        errors.Add("GOOGLE_CLIENT_ID is required (set via user secrets or an environment variable) - Gmail OAuth cannot start an authorization flow without it");
    }

    if (string.IsNullOrEmpty(configuration["GOOGLE_CLIENT_SECRET"]))
    {
        errors.Add("GOOGLE_CLIENT_SECRET is required (set via user secrets or an environment variable) - Gmail OAuth cannot exchange the authorization code without it");
    }

    // PUBLIC_URL backs the OAuth redirect_uri for both Gmail and Outlook (GmailClient/OutlookClient
    // RedirectUri()) - if unset, redirect_uri is built with a null host, which Google/Microsoft
    // reject just as opaquely as a missing client_id.
    if (string.IsNullOrEmpty(configuration["PUBLIC_URL"]))
    {
        errors.Add("PUBLIC_URL is required (set via user secrets or an environment variable) - it must be a public HTTPS URL (e.g. an ngrok tunnel in local dev) since Google/Microsoft cannot redirect to localhost");
    }

    errors.AddRange(BusinessOS.WebApi.Webhooks.WebhookConfigurationValidator.Validate(configuration));

    if (errors.Count > 0)
    {
        throw new InvalidOperationException("Invalid configuration:\n" + string.Join('\n', errors));
    }
}

static string[] BuildCorsOrigins(IConfiguration configuration, IHostEnvironment environment)
{
    // CORS_ORIGIN is a comma-separated list of the FRONTEND origins (scheme + host + port, no path).
    // It is the browser origin that matters, not the API URL: the API being reachable over both
    // http://localhost:5129 and https://localhost:7129 needs no extra entries here.
    var origins = (configuration["CORS_ORIGIN"] ?? "http://localhost:5173")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(o => o.TrimEnd('/'))
        .ToList();

    if (origins.Contains("*"))
    {
        // AllowCredentials + "any origin" is rejected by the browser and would be unsafe anyway.
        throw new InvalidOperationException("CORS_ORIGIN must list explicit origins; '*' is not allowed because credentials (cookies) are enabled");
    }

    if (environment.IsDevelopment())
    {
        // Common local variants of the Next.js dev server, so switching the frontend to https or
        // opening it via 127.0.0.1 does not need a config change.
        origins.AddRange(["https://localhost:5173", "http://127.0.0.1:5173", "https://127.0.0.1:5173"]);
    }

    return origins.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}

public partial class Program;
