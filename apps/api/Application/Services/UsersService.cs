using System.Security.Cryptography;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Application.Services;

public class UsersService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IBackgroundTaskQueue backgroundQueue,
    IAuditLogService auditLog,
    INotificationService notifications,
    ILimitEnforcementService limitEnforcement) : IUsersService
{
    // Matches AuthService's registration verification code lifetime - a 6-digit code needs a
    // short window since it's guessable, unlike the old 256-bit raw invite token.
    private static readonly TimeSpan InviteTokenLifetime = TimeSpan.FromMinutes(15);

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

    // Only for a user with no password yet (invited, logged in via code) - proving identity
    // was already done by the code login, so no current-password check applies here.
    public async Task SetPasswordAsync(string userId, SetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindByIdAsync(userId, cancellationToken);
        if (user.PasswordHash is not null)
        {
            throw new ConflictException("Password already set - use change password instead");
        }

        user.PasswordHash = await passwordHasher.HashAsync(request.NewPassword);
        auditLog.Log(user.CompanyId, userId, "security.password_set", "User", userId);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindByIdAsync(userId, cancellationToken);
        if (user.PasswordHash is null)
        {
            throw new ConflictException("No password set yet - use set password instead");
        }

        if (!await passwordHasher.VerifyAsync(user.PasswordHash, request.CurrentPassword))
        {
            throw new UnauthorizedAppException("Current password is incorrect");
        }

        user.PasswordHash = await passwordHasher.HashAsync(request.NewPassword);
        auditLog.Log(user.CompanyId, userId, "security.password_changed", "User", userId);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var users = await db.Users.Where(u => u.CompanyId == companyId).ToListAsync(cancellationToken);
        return users.Select(u => AuthDtoMap(u)).ToList();
    }

    public async Task<UserDto> GetOneAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var user = await FindInCompanyAsync(companyId, id, cancellationToken);
        return AuthDtoMap(user);
    }

    public async Task<UserDto> InviteAsync(string companyId, string actorUserId, InviteUserRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (exists)
        {
            throw new ConflictException("A user with this email already exists");
        }

        await limitEnforcement.CheckLimitAsync(companyId, UsageMetricKeys.MaxUsers, cancellationToken);

        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken)
            ?? throw new NotFoundException("Company not found");

        var code = GenerateVerificationCode();
        var role = request.Role ?? Role.Employee;
        var user = new User
        {
            CompanyId = companyId,
            Name = request.Name,
            Email = request.Email,
            Role = role,
            RbacRoleId = await ResolveRbacRoleIdAsync(companyId, request.RbacRoleId, role, cancellationToken),
            Status = UserStatus.Invited,
            EmailVerificationCodeHash = await passwordHasher.HashAsync(code),
            EmailVerificationCodeExpiresAt = DateTime.UtcNow.Add(InviteTokenLifetime),
        };

        db.Users.Add(user);
        auditLog.Log(companyId, actorUserId, "user.invited", "User", user.Id, new { email = user.Email, role = user.Role.ToString() });
        await db.SaveChangesAsync(cancellationToken);

        await EnqueueInviteEmailAsync(user, company.Name, code);

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

        var code = GenerateVerificationCode();
        user.EmailVerificationCodeHash = await passwordHasher.HashAsync(code);
        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.Add(InviteTokenLifetime);
        await db.SaveChangesAsync(cancellationToken);

        await EnqueueInviteEmailAsync(user, company.Name, code);

        return AuthDtoMap(user);
    }

    public async Task<UserDto> UpdateAsync(string companyId, string actorUserId, Role actorRole, string id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindInCompanyAsync(companyId, id, cancellationToken);
        if (user.Role == Role.Owner && actorRole != Role.Owner)
        {
            throw new ForbiddenException("Only the owner can edit another owner's account");
        }

        ApplyUpdate(user, request);
        auditLog.Log(companyId, actorUserId, "user.updated", "User", id, new { request.Name, request.AvatarUrl, Status = request.Status?.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        return AuthDtoMap(user);
    }

    public async Task<UserDto> UpdateRoleAsync(string companyId, string actorUserId, Role actorRole, string id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var target = await FindInCompanyAsync(companyId, id, cancellationToken);
        var previousRole = target.Role;

        if (request.Role == Role.Owner)
        {
            if (actorRole != Role.Owner)
            {
                throw new ForbiddenException("Only the current owner can transfer ownership");
            }

            target.Role = Role.Owner;
            target.RbacRoleId = await ResolveRbacRoleIdAsync(companyId, request.RbacRoleId, Role.Owner, cancellationToken);

            if (target.Id != actorUserId)
            {
                var actor = await FindInCompanyAsync(companyId, actorUserId, cancellationToken);
                actor.Role = Role.Admin;
                actor.RbacRoleId = await ResolveRbacRoleIdAsync(companyId, null, Role.Admin, cancellationToken);
            }
        }
        else
        {
            if (target.Role == Role.Owner && actorRole != Role.Owner)
            {
                throw new ForbiddenException("Only the owner can change another owner's role");
            }

            target.Role = request.Role;
            target.RbacRoleId = await ResolveRbacRoleIdAsync(companyId, request.RbacRoleId, request.Role, cancellationToken);
        }

        auditLog.Log(companyId, actorUserId, "user.role_changed", "User", id, new { from = previousRole.ToString(), to = target.Role.ToString() });
        await db.SaveChangesAsync(cancellationToken);

        if (previousRole != target.Role)
        {
            await notifications.NotifyAsync(companyId, target.Id, NotificationType.RoleChanged,
                "Your role changed", $"Your role is now {target.Role}", "User", target.Id, cancellationToken);
        }

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
        auditLog.Log(companyId, actorUserId, "user.deleted", "User", id, new { email = target.Email });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<LoginHistoryDto>> GetMyLoginHistoryAsync(string userId, QueryLoginHistoryRequest query, CancellationToken cancellationToken = default) =>
        await QueryLoginHistoryAsync(db.LoginHistories.Where(l => l.UserId == userId), query, cancellationToken);

    public async Task<PagedResult<LoginHistoryDto>> GetLoginHistoryAsync(string companyId, Role actorRole, string id, QueryLoginHistoryRequest query, CancellationToken cancellationToken = default)
    {
        // Coarse [RequireModule(Users)] grants Read to every role - this endpoint carries
        // IPs/device info, so real authorization (Owner/Admin only) happens here.
        if (actorRole is not (Role.Owner or Role.Admin))
        {
            throw new ForbiddenException("Only Owner/Admin can view another user's login history");
        }

        await FindInCompanyAsync(companyId, id, cancellationToken);
        return await QueryLoginHistoryAsync(db.LoginHistories.Where(l => l.UserId == id), query, cancellationToken);
    }

    private static async Task<PagedResult<LoginHistoryDto>> QueryLoginHistoryAsync(IQueryable<LoginHistory> source, QueryLoginHistoryRequest query, CancellationToken cancellationToken)
    {
        var total = await source.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await source
            .OrderByDescending(l => l.LoginAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<LoginHistoryDto>(items.Select(ToLoginHistoryDto).ToList(), page, limit, total);
    }

    private static LoginHistoryDto ToLoginHistoryDto(LoginHistory l) => new(
        l.Id, l.LoginAt, l.IpAddress, l.UserAgent, l.DeviceInfo, l.Success, l.FailureReason, l.IsNewDevice);

    // RbacRoleId wins when explicitly provided (must belong to this company); otherwise the
    // legacy Role enum maps onto its seeded system role - see SystemRoles.ForLegacyRole.
    private async Task<string?> ResolveRbacRoleIdAsync(string companyId, string? requestedRbacRoleId, Role legacyRole, CancellationToken cancellationToken)
    {
        if (requestedRbacRoleId is not null)
        {
            var exists = await db.Roles.AnyAsync(r => r.Id == requestedRbacRoleId && r.CompanyId == companyId, cancellationToken);
            if (!exists)
            {
                throw new NotFoundException("Role not found in this company");
            }

            return requestedRbacRoleId;
        }

        var systemRoleName = SystemRoles.ForLegacyRole(legacyRole);
        return await db.Roles
            .Where(r => r.CompanyId == companyId && r.Name == systemRoleName)
            .Select(r => r.Id)
            .SingleOrDefaultAsync(cancellationToken);
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

    public async Task<BulkInviteResult> BulkInviteAsync(string companyId, string actorUserId, IReadOnlyList<InviteUserRequest> users, CancellationToken cancellationToken = default)
    {
        var results = new List<BulkInviteResultItem>();
        foreach (var request in users)
        {
            try
            {
                var invited = await InviteAsync(companyId, actorUserId, request, cancellationToken);
                results.Add(new BulkInviteResultItem(request.Email, true, null, invited.Id));
            }
            catch (Exception ex) when (ex is NotFoundException or ConflictException or ValidationAppException or ForbiddenException or LimitExceededException)
            {
                results.Add(new BulkInviteResultItem(request.Email, false, ex.Message, null));
            }
        }

        return BulkInviteResult.From(results);
    }

    public async Task<string> ExportCsvAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var users = await db.Users.Where(u => u.CompanyId == companyId).OrderBy(u => u.CreatedAt).ToListAsync(cancellationToken);

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Name,Email,Role,Status");
        foreach (var u in users)
        {
            csv.AppendLine(string.Join(',', CsvUtils.Field(u.Name), CsvUtils.Field(u.Email), CsvUtils.Field(u.Role.ToString()), CsvUtils.Field(u.Status.ToString())));
        }

        return csv.ToString();
    }

    // Dedup by Email - reuses InviteAsync directly (same uniqueness guard, same invite-email
    // side effect), so a row whose email already exists surfaces as InviteAsync's
    // ConflictException, which is treated as Skipped rather than an error.
    public async Task<ImportResultDto> ImportCsvAsync(string companyId, string actorUserId, string csvContent, CancellationToken cancellationToken = default)
    {
        var rows = CsvUtils.ParseRows(csvContent);
        if (rows.Count == 0)
        {
            return new ImportResultDto(0, 0, []);
        }

        var imported = 0;
        var skipped = 0;
        var errors = new List<ImportErrorDto>();

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 1;

            if (row.Length < 2 || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1]))
            {
                errors.Add(new ImportErrorDto(rowNumber, "Name and Email are required"));
                continue;
            }

            var role = row.Length > 2 && Enum.TryParse<Role>(row[2], true, out var parsedRole) ? (Role?)parsedRole : null;

            try
            {
                await InviteAsync(companyId, actorUserId, new InviteUserRequest(row[0], row[1], role), cancellationToken);
                imported++;
            }
            catch (ConflictException)
            {
                skipped++;
            }
            catch (Exception ex) when (ex is NotFoundException or ValidationAppException or LimitExceededException)
            {
                errors.Add(new ImportErrorDto(rowNumber, ex.Message));
            }
        }

        return new ImportResultDto(imported, skipped, errors);
    }

    private async Task EnqueueInviteEmailAsync(User user, string companyName, string code)
    {
        await backgroundQueue.QueueAsync(async (sp, ct) =>
        {
            var mail = sp.GetRequiredService<IMailService>();
            await mail.SendInviteEmailAsync(user.Email, user.Name, companyName, code, ct);
        });
    }

    private static string GenerateVerificationCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static UserDto AuthDtoMap(User user) => new(
        user.Id,
        user.CompanyId,
        user.Name,
        user.Email,
        user.Role.ToString(),
        user.AvatarUrl,
        user.Status.ToString(),
        user.LastLoginAt,
        user.CreatedAt,
        user.PasswordHash is not null);
}
