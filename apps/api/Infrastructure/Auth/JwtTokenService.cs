using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BusinessOS.Infrastructure.Auth;

public class JwtTokenService : ITokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private readonly string _accessSecret;
    private readonly string _refreshSecret;

    public JwtTokenService(IConfiguration configuration)
    {
        _accessSecret = configuration["JWT_ACCESS_SECRET"]
            ?? throw new InvalidOperationException("JWT_ACCESS_SECRET is not configured");
        _refreshSecret = configuration["JWT_REFRESH_SECRET"]
            ?? throw new InvalidOperationException("JWT_REFRESH_SECRET is not configured");
    }

    public string CreateAccessToken(string userId, string companyId, Role role)
    {
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("companyId", companyId),
            new Claim("role", role.ToString()),
        };
        return CreateToken(claims, _accessSecret, AccessTokenLifetime);
    }

    public (string Token, string Jti) CreateRefreshToken(string userId)
    {
        var jti = Guid.NewGuid().ToString();
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("jti", jti),
        };
        return (CreateToken(claims, _refreshSecret, RefreshTokenLifetime), jti);
    }

    public RefreshTokenPayload? ValidateRefreshToken(string token)
    {
        // MapInboundClaims defaults to true, which silently remaps short claim
        // names like "sub" to legacy long-form URIs, so the "sub"/"jti" lookups
        // below would return null and every refresh would fail validation.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_refreshSecret)),
            ClockSkew = TimeSpan.Zero,
        };

        try
        {
            var principal = handler.ValidateToken(token, parameters, out _);
            var userId = principal.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            var jti = principal.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
            return userId is null || jti is null ? null : new RefreshTokenPayload(userId, jti);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    public RefreshTokenPayload? DecodeRefreshTokenUnsafe(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var userId = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            var jti = jwt.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
            return userId is null || jti is null ? null : new RefreshTokenPayload(userId, jti);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return null;
        }
    }

    private static string CreateToken(IEnumerable<Claim> claims, string secret, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
