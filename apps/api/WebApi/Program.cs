using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BusinessOS.Application;
using BusinessOS.Infrastructure;
using BusinessOS.WebApi.Authorization;
using BusinessOS.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

ValidateRequiredConfiguration(builder.Configuration);

var corsOrigins = (builder.Configuration["CORS_ORIGIN"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
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

app.UseCors("default");
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

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

    if (errors.Count > 0)
    {
        throw new InvalidOperationException("Invalid configuration:\n" + string.Join('\n', errors));
    }
}

public partial class Program;
