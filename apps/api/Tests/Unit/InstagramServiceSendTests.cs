using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Instagram;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class InstagramServiceSendTests
{
    [Fact]
    public async Task SendAsync_HappyPath_MarksSentAndStoresProviderMessageId()
    {
        using var db = new TestDb();
        var (service, client, notifier) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id);
        SeedAccount(db, company.Id);
        SeedConversationLink(db, company.Id, contact.Id, igsid: "ig-scoped-1");

        client.NextMessageId = "mid.123";

        await service.SendAsync(company.Id, new SendInstagramMessageRequest(contact.Id, "Salom"));

        var communication = Assert.Single(db.Context.Communications);
        Assert.Equal(CommunicationStatus.Sent, communication.Status);
        Assert.Equal("mid.123", communication.ProviderMessageId);
        Assert.Single(notifier.Notified);
    }

    [Fact]
    public async Task SendAsync_ContactHasNoConversationLink_ThrowsBadRequest()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id);
        SeedAccount(db, company.Id);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SendAsync(company.Id, new SendInstagramMessageRequest(contact.Id, "Salom")));
    }

    [Fact]
    public async Task SendAsync_ContactBelongsToAnotherCompany_ThrowsNotFound()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var otherCompany = Seed(db);
        var contact = SeedContact(db, otherCompany.Id);
        SeedAccount(db, company.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SendAsync(company.Id, new SendInstagramMessageRequest(contact.Id, "Salom")));
    }

    [Fact]
    public async Task SendAsync_NoAccountConnected_ThrowsBadRequest()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id);
        SeedConversationLink(db, company.Id, contact.Id, igsid: "ig-scoped-1");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SendAsync(company.Id, new SendInstagramMessageRequest(contact.Id, "Salom")));
    }

    [Fact]
    public async Task SendAsync_ProviderAuthExpired_ThrowsUnauthorizedAndMarksFailed()
    {
        using var db = new TestDb();
        var (service, client, notifier) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id);
        SeedAccount(db, company.Id);
        SeedConversationLink(db, company.Id, contact.Id, igsid: "ig-scoped-1");

        client.NextSendException = new ExternalServiceException("Error validating access token") { ProviderStatusCode = 401 };

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.SendAsync(company.Id, new SendInstagramMessageRequest(contact.Id, "Salom")));

        var communication = Assert.Single(db.Context.Communications);
        Assert.Equal(CommunicationStatus.Failed, communication.Status);
        Assert.Single(notifier.Notified);
    }

    [Fact]
    public async Task SendAsync_ProviderRateLimited_ThrowsRateLimited()
    {
        using var db = new TestDb();
        var (service, client, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id);
        SeedAccount(db, company.Id);
        SeedConversationLink(db, company.Id, contact.Id, igsid: "ig-scoped-1");

        client.NextSendException = new ExternalServiceException("rate limited") { ProviderStatusCode = 429, RetryAfterSeconds = 60 };

        var ex = await Assert.ThrowsAsync<RateLimitedException>(() =>
            service.SendAsync(company.Id, new SendInstagramMessageRequest(contact.Id, "Salom")));

        Assert.Equal(60, ex.RetryAfterSeconds);
    }

    private static (InstagramService Service, FakeInstagramApiClient Client, FakeRealtimeNotifier Notifier) BuildService(TestDb db)
    {
        var client = new FakeInstagramApiClient();
        var notifier = new FakeRealtimeNotifier();
        var notifications = new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance);
        var service = new InstagramService(
            db.Context,
            client,
            new FakeTokenCipher(),
            new FakeOAuthStateService(),
            notifier,
            notifications,
            NullLogger<InstagramService>.Instance);
        return (service, client, notifier);
    }

    private static Company Seed(TestDb db)
    {
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();
        return company;
    }

    private static Contact SeedContact(TestDb db, string companyId)
    {
        var contact = new Contact { CompanyId = companyId, FirstName = "Jamshed" };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();
        return contact;
    }

    private static InstagramAccount SeedAccount(TestDb db, string companyId)
    {
        var account = new InstagramAccount
        {
            CompanyId = companyId,
            InstagramBusinessAccountId = "ig-business-1",
            Username = "acme",
            EncryptedAccessToken = new FakeTokenCipher().Encrypt("token"),
            IsActive = true,
        };
        db.Context.InstagramAccounts.Add(account);
        db.Context.SaveChanges();
        return account;
    }

    private static InstagramConversationLink SeedConversationLink(TestDb db, string companyId, string contactId, string igsid)
    {
        var link = new InstagramConversationLink { CompanyId = companyId, ContactId = contactId, InstagramScopedId = igsid };
        db.Context.InstagramConversationLinks.Add(link);
        db.Context.SaveChanges();
        return link;
    }
}
