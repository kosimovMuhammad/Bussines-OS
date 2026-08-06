using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Auth;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<AuthTokens> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await db.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (exists)
        {
            throw new ConflictException("A user with this email already exists");
        }

        var company = new Company { Name = request.CompanyName };
        var user = new User
        {
            CompanyId = company.Id,
            Company = company,
            Name = request.Name,
            Email = request.Email,
            PasswordHash = await passwordHasher.HashAsync(request.Password),
            Role = Role.Owner,
        };

        db.Companies.Add(company);
        db.Users.Add(user);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user is null || user.PasswordHash is null)
        {
            throw new UnauthorizedAppException("Invalid email or password");
        }

        if (user.Status == UserStatus.Suspended)
        {
            throw new UnauthorizedAppException("This account has been suspended");
        }

        if (!await passwordHasher.VerifyAsync(user.PasswordHash, request.Password))
        {
            throw new UnauthorizedAppException("Invalid email or password");
        }

        user.LastLoginAt = DateTime.UtcNow;

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthTokens> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken cancellationToken = default)
    {
        var separatorIndex = request.Token.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex == request.Token.Length - 1)
        {
            throw new UnauthorizedAppException("Invalid or expired invite token");
        }

        var userId = request.Token[..separatorIndex];
        var rawToken = request.Token[(separatorIndex + 1)..];

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || user.Status != UserStatus.Invited || user.InviteTokenHash is null)
        {
            throw new UnauthorizedAppException("Invalid or expired invite token");
        }

        if (user.InviteTokenExpiresAt is null || user.InviteTokenExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("Invalid or expired invite token");
        }

        if (!await passwordHasher.VerifyAsync(user.InviteTokenHash, rawToken))
        {
            throw new UnauthorizedAppException("Invalid or expired invite token");
        }

        user.PasswordHash = await passwordHasher.HashAsync(request.Password);
        user.Status = UserStatus.Active;
        user.InviteTokenHash = null;
        user.InviteTokenExpiresAt = null;
        user.LastLoginAt = DateTime.UtcNow;

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthTokens> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var payload = tokenService.ValidateRefreshToken(rawRefreshToken);
        if (payload is null)
        {
            throw new UnauthorizedAppException("Invalid or expired refresh token");
        }

        var record = await db.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.Id == payload.Jti, cancellationToken);

        if (record is null || record.Revoked || record.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("Invalid or expired refresh token");
        }

        if (!await passwordHasher.VerifyAsync(record.TokenHash, rawRefreshToken))
        {
            throw new UnauthorizedAppException("Invalid or expired refresh token");
        }

        record.Revoked = true;

        return await IssueTokensAsync(record.User, cancellationToken);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var payload = tokenService.DecodeRefreshTokenUnsafe(rawRefreshToken);
        if (payload is null)
        {
            return;
        }

        var record = await db.RefreshTokens.SingleOrDefaultAsync(rt => rt.Id == payload.Jti, cancellationToken);
        if (record is not null)
        {
            record.Revoked = true;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task LogoutAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var records = await db.RefreshTokens.Where(rt => rt.UserId == userId && !rt.Revoked).ToListAsync(cancellationToken);
        foreach (var record in records)
        {
            record.Revoked = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthTokens> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = tokenService.CreateAccessToken(user.Id, user.CompanyId, user.Role);
        var (refreshToken, jti) = tokenService.CreateRefreshToken(user.Id);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = jti,
            UserId = user.Id,
            TokenHash = await passwordHasher.HashAsync(refreshToken),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime),
        });

        await db.SaveChangesAsync(cancellationToken);

        return new AuthTokens(accessToken, refreshToken, ToDto(user));
    }

    internal static UserDto ToDto(User user) => new(
        user.Id,
        user.CompanyId,
        user.Name,
        user.Email,
        user.Role.ToString(),
        user.AvatarUrl,
        user.Status.ToString(),
        user.LastLoginAt,
        user.CreatedAt);
}
