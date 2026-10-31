using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BusinessOS.Application;
using BusinessOS.Infrastructure;
using BusinessOS.Infrastructure.Realtime;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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

// Background jobs (token refresh, workflow scheduler, ...) are secondary to serving requests and
// webhooks. The default, StopHost, means a single faulted job takes the whole API down. Ignore
// keeps the API up; the job base classes already catch and log every tick failure, so reaching this
// safety net means a bug, which is logged as an error by the host.
builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

ValidateRequiredConfiguration(builder.Configuration);

var corsOrigins = (builder.Configuration["CORS_ORIGIN"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

app.MapControllers();
app.MapHub<MessagingHub>("/hubs/messaging");

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

    if (errors.Count > 0)
    {
        throw new InvalidOperationException("Invalid configuration:\n" + string.Join('\n', errors));
    }
}

public partial class Program;
