using System.Security.Cryptography;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Constants;
using BusinessOS.Application.ViewModels.Auth;
using BusinessOS.Application.ViewModels.Users;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Application.Services;

public class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IRoleSeedingService roleSeeding,
    IAuditLogService auditLog,
    IBackgroundTaskQueue backgroundQueue) : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan VerificationCodeLifetime = TimeSpan.FromMinutes(15);

    // New companies start on a 14-day trial of Pro (not Free) - standard SaaS pattern,
    // shows full product value before the company has to decide. "plan-pro" is the fixed
    // id from the SeedSubscriptionPlans migration (see Billing Phase 2). Falling back to
    // Free at trial end is not implemented yet - that's Phase 5's (Upgrade/Downgrade/
    // Cancel) territory.
    private const string DefaultTrialPlanId = "plan-pro";
    private static readonly TimeSpan TrialDuration = TimeSpan.FromDays(14);

    public async Task<RegistrationPendingDto> RegisterAsync(RegisterRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
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
            Status = UserStatus.PendingVerification,
            OnboardingRole = request.Role,
        };

        db.Companies.Add(company);
        db.Users.Add(user);
        // Roles reference companies via FK, so the company row must exist before seeding -
        // hence this early save instead of one SaveChanges at the end.
        await db.SaveChangesAsync(cancellationToken);

        var roles = await roleSeeding.SeedDefaultRolesAsync(company.Id, cancellationToken);
        user.RbacRoleId = roles[SystemRoles.Owner].Id;

        var now = DateTime.UtcNow;
        db.Subscriptions.Add(new Subscription
        {
            CompanyId = company.Id,
            PlanId = DefaultTrialPlanId,
            Status = SubscriptionStatus.Trialing,
            BillingCycle = BillingCycle.Monthly,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.Add(TrialDuration),
            TrialEndsAt = now.Add(TrialDuration),
        });

        await IssueVerificationCodeAsync(user, cancellationToken);

        return new RegistrationPendingDto(user.Email, true);
    }

    public async Task<AuthTokens> VerifyEmailAsync(VerifyEmailRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user is null || user.Status != UserStatus.PendingVerification || user.EmailVerificationCodeHash is null)
        {
            throw new UnauthorizedAppException("Invalid or expired verification code");
        }

        if (user.EmailVerificationCodeExpiresAt is null || user.EmailVerificationCodeExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("Invalid or expired verification code");
        }

        if (!await passwordHasher.VerifyAsync(user.EmailVerificationCodeHash, request.Code))
        {
            throw new UnauthorizedAppException("Invalid or expired verification code");
        }

        user.Status = UserStatus.Active;
        user.EmailVerificationCodeHash = null;
        user.EmailVerificationCodeExpiresAt = null;
        user.LastLoginAt = DateTime.UtcNow;

        auditLog.Log(user.CompanyId, user.Id, "security.email_verified", "User", user.Id, ipAddress: ipAddress, userAgent: userAgent);

        return await IssueTokensAsync(user, cancellationToken, ipAddress, userAgent);
    }

    public async Task<RegistrationPendingDto> ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user is null || user.Status != UserStatus.PendingVerification)
        {
            // Don't reveal whether the account exists/is already verified - same response either way.
            return new RegistrationPendingDto(request.Email, true);
        }

        await IssueVerificationCodeAsync(user, cancellationToken);
        return new RegistrationPendingDto(user.Email, true);
    }

    private async Task IssueVerificationCodeAsync(User user, CancellationToken cancellationToken)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.EmailVerificationCodeHash = await passwordHasher.HashAsync(code);
        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.Add(VerificationCodeLifetime);
        await db.SaveChangesAsync(cancellationToken);

        var email = user.Email;
        var name = user.Name;
        await backgroundQueue.QueueAsync(async (sp, ct) =>
        {
            var mail = sp.GetRequiredService<IMailService>();
            await mail.SendVerificationCodeEmailAsync(email, name, code, ct);
        });
    }

    public async Task<AuthTokens> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user is null || user.PasswordHash is null)
        {
            // No AuditLog entry here (unlike the other failure branches below) - AuditLog is
            // company-scoped (FK to companies) and an unknown email resolves to no company at
            // all, same reasoning as LoginHistory.CompanyId being nullable/unset in this case.
            await RecordLoginAttemptAsync(null, null, ipAddress, userAgent, success: false, failureReason: "invalid_email", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException("Invalid email or password");
        }

        if (user.Status == UserStatus.Suspended)
        {
            await RecordLoginAttemptAsync(user.Id, user.CompanyId, ipAddress, userAgent, success: false, failureReason: "account_suspended", cancellationToken);
            auditLog.Log(user.CompanyId, user.Id, "security.login_failed", "User", user.Id, new { reason = "account_suspended" }, ipAddress, userAgent);
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException("This account has been suspended");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            await RecordLoginAttemptAsync(user.Id, user.CompanyId, ipAddress, userAgent, success: false, failureReason: "pending_verification", cancellationToken);
            auditLog.Log(user.CompanyId, user.Id, "security.login_failed", "User", user.Id, new { reason = "pending_verification" }, ipAddress, userAgent);
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException("Please verify your email before logging in");
        }

        if (!await passwordHasher.VerifyAsync(user.PasswordHash, request.Password))
        {
            await RecordLoginAttemptAsync(user.Id, user.CompanyId, ipAddress, userAgent, success: false, failureReason: "invalid_password", cancellationToken);
            auditLog.Log(user.CompanyId, user.Id, "security.login_failed", "User", user.Id, new { reason = "invalid_password" }, ipAddress, userAgent);
            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException("Invalid email or password");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await RecordLoginAttemptAsync(user.Id, user.CompanyId, ipAddress, userAgent, success: true, failureReason: null, cancellationToken);
        auditLog.Log(user.CompanyId, user.Id, "security.login_succeeded", "User", user.Id, ipAddress: ipAddress, userAgent: userAgent);

        return await IssueTokensAsync(user, cancellationToken, ipAddress, userAgent);
    }

    // Invited users have no password yet - the code itself is the credential. Successful
    // login here is their account activation; setting a password is a separate, later,
    // self-service step from Settings (see UsersService.SetPasswordAsync).
    public async Task<AuthTokens> LoginWithCodeAsync(LoginWithCodeRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user is null || user.Status != UserStatus.Invited || user.EmailVerificationCodeHash is null)
        {
            throw new UnauthorizedAppException("Invalid or expired code");
        }

        if (user.EmailVerificationCodeExpiresAt is null || user.EmailVerificationCodeExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("Invalid or expired code");
        }

        if (!await passwordHasher.VerifyAsync(user.EmailVerificationCodeHash, request.Code))
        {
            throw new UnauthorizedAppException("Invalid or expired code");
        }

        user.Status = UserStatus.Active;
        user.EmailVerificationCodeHash = null;
        user.EmailVerificationCodeExpiresAt = null;
        user.LastLoginAt = DateTime.UtcNow;

        auditLog.Log(user.CompanyId, user.Id, "security.invite_login", "User", user.Id, ipAddress: ipAddress, userAgent: userAgent);

        return await IssueTokensAsync(user, cancellationToken, ipAddress, userAgent);
    }

    public async Task<RegistrationPendingDto> ResendInviteCodeAsync(ResendInviteCodeRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user is null || user.Status != UserStatus.Invited)
        {
            // Don't reveal whether the account exists/is already active - same response either way.
            return new RegistrationPendingDto(request.Email, true);
        }

        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == user.CompanyId, cancellationToken);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.EmailVerificationCodeHash = await passwordHasher.HashAsync(code);
        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.Add(VerificationCodeLifetime);
        await db.SaveChangesAsync(cancellationToken);

        var email = user.Email;
        var name = user.Name;
        var companyName = company?.Name ?? string.Empty;
        await backgroundQueue.QueueAsync(async (sp, ct) =>
        {
            var mail = sp.GetRequiredService<IMailService>();
            await mail.SendInviteEmailAsync(email, name, companyName, code, ct);
        });

        return new RegistrationPendingDto(user.Email, true);
    }

    public async Task<AuthTokens> RefreshAsync(string rawRefreshToken, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
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

        return await IssueTokensAsync(record.User, cancellationToken, ipAddress, userAgent);
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

    public async Task LogoutAllAsync(string companyId, string userId, CancellationToken cancellationToken = default)
    {
        var records = await db.RefreshTokens.Where(rt => rt.UserId == userId && !rt.Revoked).ToListAsync(cancellationToken);
        foreach (var record in records)
        {
            record.Revoked = true;
        }

        auditLog.Log(companyId, userId, "security.sessions_revoked", "User", userId);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordLoginAttemptAsync(
        string? userId, string? companyId, string? ipAddress, string? userAgent, bool success, string? failureReason, CancellationToken cancellationToken)
    {
        var isNewDevice = success && userId is not null && await IsNewDeviceAsync(userId, ipAddress, userAgent, cancellationToken);

        db.LoginHistories.Add(new LoginHistory
        {
            UserId = userId,
            CompanyId = companyId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = ParseDeviceInfo(userAgent),
            Success = success,
            FailureReason = failureReason,
            IsNewDevice = isNewDevice,
        });
    }

    private async Task<bool> IsNewDeviceAsync(string userId, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        if (ipAddress is null && userAgent is null)
        {
            return true;
        }

        var seenBefore = await db.LoginHistories.AnyAsync(l =>
            l.UserId == userId && l.Success &&
            ((ipAddress != null && l.IpAddress == ipAddress) || (userAgent != null && l.UserAgent == userAgent)),
            cancellationToken);

        return !seenBefore;
    }

    // Lightweight regex-free parse - not a full UA-parsing library, just enough for the
    // frontend to show "Chrome / Windows" style device labels.
    private static string? ParseDeviceInfo(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var os = userAgent switch
        {
            _ when userAgent.Contains("Windows") => "Windows",
            _ when userAgent.Contains("Mac OS X") || userAgent.Contains("Macintosh") => "macOS",
            _ when userAgent.Contains("Android") => "Android",
            _ when userAgent.Contains("iPhone") || userAgent.Contains("iPad") => "iOS",
            _ when userAgent.Contains("Linux") => "Linux",
            _ => "Unknown",
        };

        var browser = userAgent switch
        {
            _ when userAgent.Contains("Edg/") => "Edge",
            _ when userAgent.Contains("Chrome/") && !userAgent.Contains("Edg/") => "Chrome",
            _ when userAgent.Contains("Firefox/") => "Firefox",
            _ when userAgent.Contains("Safari/") && !userAgent.Contains("Chrome/") => "Safari",
            _ => "Unknown",
        };

        return $"{browser} / {os}";
    }

    private async Task<AuthTokens> IssueTokensAsync(User user, CancellationToken cancellationToken, string? ipAddress = null, string? userAgent = null)
    {
        var accessToken = tokenService.CreateAccessToken(user.Id, user.CompanyId, user.Role, user.BranchId);
        var (refreshToken, jti) = tokenService.CreateRefreshToken(user.Id);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = jti,
            UserId = user.Id,
            TokenHash = await passwordHasher.HashAsync(refreshToken),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = ParseDeviceInfo(userAgent),
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
        user.CoverUrl,
        user.Status.ToString(),
        user.LastLoginAt,
        user.CreatedAt,
        user.PasswordHash is not null,
        user.IsPlatformAdmin);
}
