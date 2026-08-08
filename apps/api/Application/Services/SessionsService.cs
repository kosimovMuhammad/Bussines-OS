using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Sessions;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class SessionsService(IAppDbContext db) : ISessionsService
{
    public async Task<IReadOnlyList<SessionDto>> ListAsync(string userId, string? currentSessionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.Revoked && rt.ExpiresAt > now)
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync(cancellationToken);

        return tokens
            .Select(rt => new SessionDto(rt.Id, rt.DeviceInfo, rt.IpAddress, rt.CreatedAt, rt.ExpiresAt, rt.Id == currentSessionId))
            .ToList();
    }

    public async Task RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        var token = await db.RefreshTokens.SingleOrDefaultAsync(rt => rt.Id == sessionId && rt.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Session not found");

        token.Revoked = true;
        await db.SaveChangesAsync(cancellationToken);
    }
}
