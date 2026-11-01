using System.Text.Json;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Instagram;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Instagram;
using BusinessOS.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.Tests.Unit;

// Regression tests for "every webhook dropped": /me "user_id" (snake_case) was never bound to the
// DTO, so the app-scoped "id" was stored instead of the professional id webhooks carry.
public class InstagramIdentityTests
{
    private const string AppScoped = "100000000000001";
    private const string Professional = "17841400000000001";
    private const string Customer = "17900000000000001";

    [Fact]
    public void ParseAccountInfo_BindsSnakeCaseUserId()
    {
        var info = InstagramOAuthParser.ParseAccountInfo($$"""{"id":"{{AppScoped}}","user_id":"{{Professional}}","username":"acme_shop"}""");
        Assert.Equal(AppScoped, info.Id);
        Assert.Equal(Professional, info.UserId);
        Assert.Equal("acme_shop", info.Username);
    }

    [Fact]
    public void ParseAccountInfo_AcceptsNumericIds_AndMissingUserId()
    {
        var info = InstagramOAuthParser.ParseAccountInfo($$"""{"id":{{AppScoped}},"username":"x"}""");
        Assert.Equal(AppScoped, info.Id);
        Assert.Null(info.UserId);
    }

    [Fact]
    public void ParseAccountInfo_NoId_Throws() =>
        Assert.Throws<JsonException>(() => InstagramOAuthParser.ParseAccountInfo("""{"username":"x"}"""));

    [Fact]
    public async Task Connect_StoresBothIds_ProfessionalAsAccountId()
    {
        using var db = new TestDb();
        var (service, client) = Build(db);
        var company = SeedCompany(db);
        client.NextAccountInfo = new InstagramUserInfo(AppScoped, Professional, "acme_shop", null);

        await service.ConnectAccountAsync(company.Id, new ConnectInstagramAccountRequest("tok"));

        var account = Assert.Single(db.Context.InstagramAccounts);
        Assert.Equal(Professional, account.InstagramBusinessAccountId);
        Assert.Equal(AppScoped, account.AppScopedUserId);
        Assert.Equal([Professional], client.SubscribedAccountIds);
    }

    [Fact]
    public async Task Connect_WithoutUserId_FailsInsteadOfStoringAppScopedId()
    {
        using var db = new TestDb();
        var (service, client) = Build(db);
        var company = SeedCompany(db);
        client.NextAccountInfo = new InstagramUserInfo(AppScoped, null, "acme_shop", null);

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            service.ConnectAccountAsync(company.Id, new ConnectInstagramAccountRequest("tok")));
        Assert.Empty(db.Context.InstagramAccounts);
    }

    [Fact]
    public async Task Reconnect_RepairsLegacyRowInPlace()
    {
        using var db = new TestDb();
        var (service, client) = Build(db);
        var company = SeedCompany(db);
        var legacy = SeedAccount(db, company.Id, businessId: AppScoped, appScoped: null);
        client.NextAccountInfo = new InstagramUserInfo(AppScoped, Professional, "acme_shop", null);

        await service.ConnectAccountAsync(company.Id, new ConnectInstagramAccountRequest("tok"));

        var account = Assert.Single(db.Context.InstagramAccounts);
        Assert.Equal(legacy.Id, account.Id);
        Assert.Equal(Professional, account.InstagramBusinessAccountId);
        Assert.Equal(AppScoped, account.AppScopedUserId);
    }

    [Fact]
    public async Task Webhook_InboundMessage_MatchedByEntryId()
    {
        using var db = new TestDb();
        var (service, _) = Build(db);
        var company = SeedCompany(db);
        SeedAccount(db, company.Id, Professional, AppScoped);

        await service.ProcessWebhookAsync(Payload(entryId: Professional, sender: Customer, recipient: Professional, isEcho: false));

        var communication = Assert.Single(db.Context.Communications);
        Assert.Equal("hello", communication.Content);
        Assert.Single(db.Context.InstagramConversationLinks);
    }

    [Fact]
    public async Task Webhook_Echo_IsIgnored_NoContactOrLinkCreated()
    {
        using var db = new TestDb();
        var (service, _) = Build(db);
        var company = SeedCompany(db);
        SeedAccount(db, company.Id, Professional, AppScoped);

        // Echo of a message our account sent: sender = our account, recipient = the customer.
        await service.ProcessWebhookAsync(Payload(entryId: Professional, sender: Professional, recipient: Customer, isEcho: true));

        Assert.Empty(db.Context.Communications);
        Assert.Empty(db.Context.InstagramConversationLinks);
        Assert.Empty(db.Context.Contacts);
    }

    [Fact]
    public async Task Webhook_EventForUnknownAccount_IsDropped()
    {
        using var db = new TestDb();
        var (service, _) = Build(db);
        var company = SeedCompany(db);
        SeedAccount(db, company.Id, Professional, AppScoped);

        await service.ProcessWebhookAsync(Payload(entryId: Customer, sender: "1", recipient: Customer, isEcho: false));

        Assert.Empty(db.Context.Communications);
    }

    [Fact]
    public async Task Deauthorize_MatchesAppScopedIdToo()
    {
        using var db = new TestDb();
        var (service, _) = Build(db);
        var company = SeedCompany(db);
        SeedAccount(db, company.Id, Professional, AppScoped);

        Assert.True(await service.HandleDeauthorizeAsync(AppScoped));
        Assert.Empty(db.Context.InstagramAccounts);
    }

    [Fact]
    public async Task TokenRefresh_BackfillsLegacyAccountIds_AndResubscribes()
    {
        using var db = new TestDb();
        var client = new FakeInstagramApiClient { NextAccountInfo = new InstagramUserInfo(AppScoped, Professional, "acme_shop", null) };
        var company = SeedCompany(db);
        SeedAccount(db, company.Id, businessId: AppScoped, appScoped: null);
        var refresh = new InstagramTokenRefreshService(db.Context, client, new FakeTokenCipher(), NullLogger<InstagramTokenRefreshService>.Instance);

        await refresh.RefreshDueTokensAsync();

        var account = await db.Context.InstagramAccounts.SingleAsync();
        Assert.Equal(Professional, account.InstagramBusinessAccountId);
        Assert.Equal(AppScoped, account.AppScopedUserId);
        Assert.Equal([Professional], client.SubscribedAccountIds);

        // Idempotent: a second tick has nothing left to backfill.
        client.SubscribedAccountIds.Clear();
        await refresh.RefreshDueTokensAsync();
        Assert.Empty(client.SubscribedAccountIds);
    }

    private static string Payload(string entryId, string sender, string recipient, bool isEcho)
    {
        var echo = isEcho ? "true" : "false";
        var mid = Guid.NewGuid().ToString("N");
        return $$$"""
            {"object":"instagram","entry":[{"id":"{{{entryId}}}","time":1700000000,"messaging":[
              {"sender":{"id":"{{{sender}}}"},"recipient":{"id":"{{{recipient}}}"},"timestamp":1700000000000,
               "message":{"mid":"mid.{{{mid}}}","text":"hello","is_echo":{{{echo}}}}}
            ]}]}
            """;
    }

    private static Company SeedCompany(TestDb db)
    {
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();
        return company;
    }

    private static InstagramAccount SeedAccount(TestDb db, string companyId, string businessId, string? appScoped)
    {
        var account = new InstagramAccount
        {
            CompanyId = companyId,
            InstagramBusinessAccountId = businessId,
            AppScopedUserId = appScoped,
            Username = "acme_shop",
            EncryptedAccessToken = new FakeTokenCipher().Encrypt("token"),
        };
        db.Context.InstagramAccounts.Add(account);
        db.Context.SaveChanges();
        return account;
    }

    private static (InstagramService Service, FakeInstagramApiClient Client) Build(TestDb db)
    {
        var client = new FakeInstagramApiClient();
        var service = new InstagramService(
            db.Context, client, new FakeTokenCipher(), new FakeOAuthStateService(), new FakeRealtimeNotifier(),
            new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance),
            NullLogger<InstagramService>.Instance, new AuditLogService(db.Context));
        return (service, client);
    }
}
