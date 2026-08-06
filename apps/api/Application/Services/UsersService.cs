using System.Security.Cryptography;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Application.Services;

public class UsersService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IBackgroundTaskQueue backgroundQueue,
    IConfiguration configuration) : IUsersService
{
    private static readonly TimeSpan InviteTokenLifetime = TimeSpan.FromDays(7);

    public async Task<UserDto> GetMeAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await FindByIdAsync(userId, cancellationToken);
        return AuthDtoMap(user);
    }

    public async Task<UserDto> UpdateMeAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindByIdAsync(userId, cancellationToken);
        ApplyUpdate(user, request);
        await db.SaveChangesAsync(cancellationToken);
        return AuthDtoMap(user);
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var users = await db.Users.Where(u => u.CompanyId == companyId).ToListAsync(cancellationToken);
        return users.Select(AuthDtoMap).ToList();
    }

    public async Task<UserDto> GetOneAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var user = await FindInCompanyAsync(companyId, id, cancellationToken);
        return AuthDtoMap(user);
    }

    public async Task<UserDto> InviteAsync(string companyId, InviteUserRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await db.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (exists)
        {
            throw new ConflictException("A user with this email already exists");
        }

        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken)
            ?? throw new NotFoundException("Company not found");

        var rawToken = GenerateRawToken();
        var user = new User
        {
            CompanyId = companyId,
            Name = request.Name,
            Email = request.Email,
            Role = request.Role ?? Role.Employee,
            Status = UserStatus.Invited,
            InviteTokenHash = await passwordHasher.HashAsync(rawToken),
            InviteTokenExpiresAt = DateTime.UtcNow.Add(InviteTokenLifetime),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        await EnqueueInviteEmailAsync(user, company.Name, rawToken);

        return AuthDtoMap(user);
    }

    public async Task<UserDto> ResendInviteAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var user = await FindInCompanyAsync(companyId, id, cancellationToken);
        if (user.Status != UserStatus.Invited)
        {
            throw new ConflictException("User is not pending an invite");
        }

        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken)
            ?? throw new NotFoundException("Company not found");

        var rawToken = GenerateRawToken();
        user.InviteTokenHash = await passwordHasher.HashAsync(rawToken);
        user.InviteTokenExpiresAt = DateTime.UtcNow.Add(InviteTokenLifetime);
        await db.SaveChangesAsync(cancellationToken);

        await EnqueueInviteEmailAsync(user, company.Name, rawToken);

        return AuthDtoMap(user);
    }

    public async Task<UserDto> UpdateAsync(string companyId, Role actorRole, string id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindInCompanyAsync(companyId, id, cancellationToken);
        if (user.Role == Role.Owner && actorRole != Role.Owner)
        {
            throw new ForbiddenException("Only the owner can edit another owner's account");
        }

        ApplyUpdate(user, request);
        await db.SaveChangesAsync(cancellationToken);
        return AuthDtoMap(user);
    }

    public async Task<UserDto> UpdateRoleAsync(string companyId, string actorUserId, Role actorRole, string id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var target = await FindInCompanyAsync(companyId, id, cancellationToken);

        if (request.Role == Role.Owner)
        {
            if (actorRole != Role.Owner)
            {
                throw new ForbiddenException("Only the current owner can transfer ownership");
            }

            target.Role = Role.Owner;

            if (target.Id != actorUserId)
            {
                var actor = await FindInCompanyAsync(companyId, actorUserId, cancellationToken);
                actor.Role = Role.Admin;
            }
        }
        else
        {
            if (target.Role == Role.Owner && actorRole != Role.Owner)
            {
                throw new ForbiddenException("Only the owner can change another owner's role");
            }

            target.Role = request.Role;
        }

        await db.SaveChangesAsync(cancellationToken);
        return AuthDtoMap(target);
    }

    public async Task RemoveAsync(string companyId, string actorUserId, string id, CancellationToken cancellationToken = default)
    {
        if (id == actorUserId)
        {
            throw new ForbiddenException("You cannot delete your own account");
        }

        var target = await FindInCompanyAsync(companyId, id, cancellationToken);
        if (target.Role == Role.Owner)
        {
            throw new ForbiddenException("Cannot delete the account owner");
        }

        db.Users.Remove(target);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new NotFoundException("User not found");

    private async Task<User> FindInCompanyAsync(string companyId, string id, CancellationToken cancellationToken) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId, cancellationToken)
        ?? throw new NotFoundException("User not found");

    private static void ApplyUpdate(User user, UpdateUserRequest request)
    {
        if (request.Name is not null)
        {
            user.Name = request.Name;
        }

        if (request.AvatarUrl is not null)
        {
            user.AvatarUrl = request.AvatarUrl;
        }

        if (request.Status is not null)
        {
            user.Status = request.Status.Value;
        }
    }

    private async Task EnqueueInviteEmailAsync(User user, string companyName, string rawToken)
    {
        var frontendUrl = configuration["FRONTEND_URL"] ?? configuration["PUBLIC_URL"] ?? string.Empty;
        var inviteLink = $"{frontendUrl.TrimEnd('/')}/accept-invite?token={user.Id}.{rawToken}";

        await backgroundQueue.QueueAsync(async (sp, ct) =>
        {
            var mail = sp.GetRequiredService<IMailService>();
            await mail.SendInviteEmailAsync(user.Email, user.Name, companyName, inviteLink, ct);
        });
    }

    private static string GenerateRawToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    private static UserDto AuthDtoMap(User user) => new(
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
