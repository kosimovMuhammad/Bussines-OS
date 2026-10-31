using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class WhatsAppServiceSendTests
{
    [Fact]
    public async Task SendAsync_HappyPath_MarksSentAndStoresProviderMessageId()
    {
        using var db = new TestDb();
        var (service, client, notifier) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, phone: "+992900000001");
        SeedAccount(db, company.Id);

        client.NextMessageId = "wamid.123";

        await service.SendAsync(company.Id, new SendWhatsAppRequest(contact.Id, "Hello"));

        var communication = Assert.Single(db.Context.Communications);
        Assert.Equal(CommunicationStatus.Sent, communication.Status);
        Assert.Equal("wamid.123", communication.ProviderMessageId);
        Assert.Single(notifier.Notified);
    }

    [Fact]
    public async Task SendAsync_ContactMissingPhone_ThrowsBadRequest()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, phone: null);
        SeedAccount(db, company.Id);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SendAsync(company.Id, new SendWhatsAppRequest(contact.Id, "Hello")));
    }

    [Fact]
    public async Task SendAsync_ContactBelongsToAnotherCompany_ThrowsNotFound()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var otherCompany = Seed(db);
        var contact = SeedContact(db, otherCompany.Id, phone: "+992900000001");
        SeedAccount(db, company.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SendAsync(company.Id, new SendWhatsAppRequest(contact.Id, "Hello")));
    }

    [Fact]
    public async Task SendAsync_NoAccountConnected_ThrowsBadRequest()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, phone: "+992900000001");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SendAsync(company.Id, new SendWhatsAppRequest(contact.Id, "Hello")));
    }

    [Fact]
    public async Task SendAsync_ProviderAuthExpired_ThrowsUnauthorizedAndMarksFailed()
    {
        using var db = new TestDb();
        var (service, client, notifier) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, phone: "+992900000001");
        SeedAccount(db, company.Id);

        client.NextSendException = new ExternalServiceException("Error validating access token") { ProviderStatusCode = 401 };

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.SendAsync(company.Id, new SendWhatsAppRequest(contact.Id, "Hello")));

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
        var contact = SeedContact(db, company.Id, phone: "+992900000001");
        SeedAccount(db, company.Id);

        client.NextSendException = new ExternalServiceException("rate limited") { ProviderStatusCode = 429, RetryAfterSeconds = 30 };

        var ex = await Assert.ThrowsAsync<RateLimitedException>(() =>
            service.SendAsync(company.Id, new SendWhatsAppRequest(contact.Id, "Hello")));

        Assert.Equal(30, ex.RetryAfterSeconds);
    }

    [Fact]
    public async Task SendAsync_ProviderTimeout_ThrowsServiceUnavailable()
    {
        using var db = new TestDb();
        var (service, client, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, phone: "+992900000001");
        SeedAccount(db, company.Id);

        client.NextSendException = new ExternalServiceException("timed out") { IsTimeout = true };

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            service.SendAsync(company.Id, new SendWhatsAppRequest(contact.Id, "Hello")));
    }

    [Fact]
    public async Task SendAsync_ProviderRejectsRecipient_ThrowsSendRejected()
    {
        using var db = new TestDb();
        var (service, client, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, phone: "+992900000001");
        SeedAccount(db, company.Id);

        client.NextSendException = new ExternalServiceException("Recipient phone number not in allowed list") { ProviderStatusCode = 400 };

        await Assert.ThrowsAsync<SendRejectedException>(() =>
            service.SendAsync(company.Id, new SendWhatsAppRequest(contact.Id, "Hello")));
    }

    private static (WhatsAppService Service, FakeWhatsAppClient Client, FakeRealtimeNotifier Notifier) BuildService(TestDb db)
    {
        var client = new FakeWhatsAppClient();
        var notifier = new FakeRealtimeNotifier();
        var notifications = new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance);
        var service = new WhatsAppService(
            db.Context,
            client,
            new FakeTokenCipher(),
            notifier,
            new AuditLogService(db.Context),
            notifications,
            NullLogger<WhatsAppService>.Instance);
        return (service, client, notifier);
    }

    private static Company Seed(TestDb db)
    {
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();
        return company;
    }

    private static Contact SeedContact(TestDb db, string companyId, string? phone)
    {
        var contact = new Contact { CompanyId = companyId, FirstName = "Jamshed", Phone = phone };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();
        return contact;
    }

    private static WhatsAppAccount SeedAccount(TestDb db, string companyId)
    {
        var account = new WhatsAppAccount
        {
            CompanyId = companyId,
            PhoneNumberId = "phone-number-id",
            WabaId = "waba-id",
            EncryptedAccessToken = new FakeTokenCipher().Encrypt("token"),
            IsActive = true,
        };
        db.Context.WhatsAppAccounts.Add(account);
        db.Context.SaveChanges();
        return account;
    }
}
