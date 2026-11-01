using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.Tests.Unit;

public class InstagramAccountLifecycleTests
{
    [Fact]
    public async Task Disconnect_RemovesAccount_KeepsConversationLinks()
    {
        using var db = new TestDb();
        var service = Build(db);
        var (companyId, accountId) = Seed(db);

        await service.DisconnectAccountAsync(companyId, UserId(db), accountId);

        Assert.Empty(db.Context.InstagramAccounts);
        Assert.Single(db.Context.InstagramConversationLinks);
    }

    [Fact]
    public async Task Disconnect_OtherCompanysAccount_ThrowsNotFound()
    {
        using var db = new TestDb();
        var service = Build(db);
        var (_, accountId) = Seed(db);
        var other = new Company { Name = "Other" };
        db.Context.Companies.Add(other);
        db.Context.SaveChanges();

        await Assert.ThrowsAsync<NotFoundException>(() => service.DisconnectAccountAsync(other.Id, UserId(db), accountId));
        Assert.Single(db.Context.InstagramAccounts);
    }

    [Fact]
    public async Task Deauthorize_RemovesAccountByInstagramUserId()
    {
        using var db = new TestDb();
        var service = Build(db);
        Seed(db);

        Assert.False(await service.HandleDeauthorizeAsync("unknown"));
        Assert.True(await service.HandleDeauthorizeAsync("ig-business-1"));
        Assert.Empty(db.Context.InstagramAccounts);
        Assert.Single(db.Context.InstagramConversationLinks);
    }

    [Fact]
    public async Task DataDeletion_RemovesAccountAndConversationLinks()
    {
        using var db = new TestDb();
        var service = Build(db);
        Seed(db);

        Assert.True(await service.HandleDataDeletionAsync("ig-business-1"));
        Assert.Empty(db.Context.InstagramAccounts);
        Assert.Empty(db.Context.InstagramConversationLinks);
        Assert.Single(await db.Context.Contacts.ToListAsync());
    }

    private static string UserId(TestDb db)
    {
        var company = db.Context.Companies.First();
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        return user.Id;
    }

    private static (string CompanyId, string AccountId) Seed(TestDb db)
    {
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();
        var contact = new Contact { CompanyId = company.Id, FirstName = "Jamshed" };
        var account = new InstagramAccount
        {
            CompanyId = company.Id,
            InstagramBusinessAccountId = "ig-business-1",
            Username = "acme",
            EncryptedAccessToken = "x",
        };
        db.Context.AddRange(contact, account,
            new InstagramConversationLink { CompanyId = company.Id, ContactId = contact.Id, InstagramScopedId = "scoped-1" });
        db.Context.SaveChanges();
        return (company.Id, account.Id);
    }

    private static InstagramService Build(TestDb db) => new(
        db.Context, new FakeInstagramApiClient(), new FakeTokenCipher(), new FakeOAuthStateService(), new FakeRealtimeNotifier(),
        new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance),
        NullLogger<InstagramService>.Instance, new AuditLogService(db.Context));
}
