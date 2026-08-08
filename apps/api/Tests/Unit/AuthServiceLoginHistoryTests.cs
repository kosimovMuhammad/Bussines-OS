using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Auth;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Tests.Unit;

public class AuthServiceLoginHistoryTests
{
    private const string Password = "correct-password";

    private static async Task<(TestDb Db, AuthService Service, Company Company, User User)> CreateSutAsync()
    {
        var db = new TestDb();
        var hasher = new FakePasswordHasher();
        var service = new AuthService(db.Context, hasher, new FakeTokenService(), new RoleSeedingService(db.Context), new AuditLogService(db.Context), new NoopBackgroundTaskQueue());

        var company = new Company { Name = "Acme" };
        var user = new User
        {
            CompanyId = company.Id,
            Company = company,
            Name = "Alice",
            Email = "alice@acme.tj",
            PasswordHash = await hasher.HashAsync(Password),
        };

        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        return (db, service, company, user);
    }

    [Fact]
    public async Task Login_Success_RecordsSuccessfulLoginHistory()
    {
        var (db, service, _, user) = await CreateSutAsync();

        await service.LoginAsync(new LoginRequest(user.Email, Password), "1.2.3.4", "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0", CancellationToken.None);

        var entry = Assert.Single(db.Context.LoginHistories.Where(l => l.UserId == user.Id));
        Assert.True(entry.Success);
        Assert.Null(entry.FailureReason);
        Assert.Equal("1.2.3.4", entry.IpAddress);
        Assert.Contains("Chrome", entry.DeviceInfo);
        Assert.Contains("Windows", entry.DeviceInfo);
    }

    [Fact]
    public async Task Login_WrongPassword_RecordsFailureWithReason()
    {
        var (db, service, _, user) = await CreateSutAsync();

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.LoginAsync(new LoginRequest(user.Email, "wrong-password"), "1.2.3.4", "ua", CancellationToken.None));

        var entry = Assert.Single(db.Context.LoginHistories.Where(l => l.UserId == user.Id));
        Assert.False(entry.Success);
        Assert.Equal("invalid_password", entry.FailureReason);
    }

    [Fact]
    public async Task Login_UnknownEmail_RecordsFailureWithoutUserId()
    {
        var (db, service, _, _) = await CreateSutAsync();

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.LoginAsync(new LoginRequest("nobody@acme.tj", "whatever"), "5.6.7.8", "ua", CancellationToken.None));

        var entry = Assert.Single(db.Context.LoginHistories.Where(l => l.IpAddress == "5.6.7.8"));
        Assert.False(entry.Success);
        Assert.Null(entry.UserId);
        Assert.Equal("invalid_email", entry.FailureReason);
    }

    [Fact]
    public async Task Login_SuspendedAccount_RecordsFailureWithReason()
    {
        var (db, service, _, user) = await CreateSutAsync();
        user.Status = UserStatus.Suspended;
        await db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.LoginAsync(new LoginRequest(user.Email, Password), "1.2.3.4", "ua", CancellationToken.None));

        var entry = Assert.Single(db.Context.LoginHistories.Where(l => l.UserId == user.Id));
        Assert.False(entry.Success);
        Assert.Equal("account_suspended", entry.FailureReason);
    }

    [Fact]
    public async Task Login_SecondTimeSameDevice_IsNotFlaggedAsNewDevice()
    {
        var (db, service, _, user) = await CreateSutAsync();

        await service.LoginAsync(new LoginRequest(user.Email, Password), "1.2.3.4", "same-ua", CancellationToken.None);
        await service.LoginAsync(new LoginRequest(user.Email, Password), "1.2.3.4", "same-ua", CancellationToken.None);

        var entries = await db.Context.LoginHistories.Where(l => l.UserId == user.Id).OrderBy(l => l.LoginAt).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].IsNewDevice);
        Assert.False(entries[1].IsNewDevice);
    }

    [Fact]
    public async Task Login_NewIpAndUserAgent_IsFlaggedAsNewDevice()
    {
        var (db, service, _, user) = await CreateSutAsync();

        await service.LoginAsync(new LoginRequest(user.Email, Password), "1.2.3.4", "ua-1", CancellationToken.None);
        await service.LoginAsync(new LoginRequest(user.Email, Password), "9.9.9.9", "ua-2", CancellationToken.None);

        var entries = await db.Context.LoginHistories.Where(l => l.UserId == user.Id).OrderBy(l => l.LoginAt).ToListAsync();
        Assert.True(entries[1].IsNewDevice);
    }
}
