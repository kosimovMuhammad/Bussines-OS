using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Telegram;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class TelegramServiceSendTests
{
    [Fact]
    public async Task SendAsync_HappyPath_MarksSentAndStoresProviderMessageId()
    {
        using var db = new TestDb();
        var (service, client, notifier) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id);
        SeedAccount(db, company.Id);
        SeedChatLink(db, company.Id, contact.Id, chatId: 12345);

        client.NextMessageId = 999;

        await service.SendAsync(company.Id, new SendTelegramMessageRequest(contact.Id, "Salom"));

        var communication = Assert.Single(db.Context.Communications);
        Assert.Equal(CommunicationStatus.Sent, communication.Status);
        Assert.Equal("999", communication.ProviderMessageId);
        Assert.Single(notifier.Notified);
    }

    [Fact]
    public async Task SendAsync_ContactHasNoChatLink_ThrowsBadRequest()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id);
        SeedAccount(db, company.Id);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SendAsync(company.Id, new SendTelegramMessageRequest(contact.Id, "Salom")));
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
            service.SendAsync(company.Id, new SendTelegramMessageRequest(contact.Id, "Salom")));
    }

    [Fact]
    public async Task SendAsync_NoBotConnected_ThrowsBadRequest()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id);
        SeedChatLink(db, company.Id, contact.Id, chatId: 12345);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SendAsync(company.Id, new SendTelegramMessageRequest(contact.Id, "Salom")));
    }

    [Fact]
    public async Task SendAsync_ProviderAuthExpired_ThrowsUnauthorizedAndMarksFailed()
    {
        using var db = new TestDb();
        var (service, client, notifier) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id);
        SeedAccount(db, company.Id);
        SeedChatLink(db, company.Id, contact.Id, chatId: 12345);

        client.NextSendException = new ExternalServiceException("Unauthorized") { ProviderStatusCode = 401 };

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.SendAsync(company.Id, new SendTelegramMessageRequest(contact.Id, "Salom")));

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
        SeedChatLink(db, company.Id, contact.Id, chatId: 12345);

        client.NextSendException = new ExternalServiceException("Too Many Requests: retry after 5") { ProviderStatusCode = 429, RetryAfterSeconds = 5 };

        var ex = await Assert.ThrowsAsync<RateLimitedException>(() =>
            service.SendAsync(company.Id, new SendTelegramMessageRequest(contact.Id, "Salom")));

        Assert.Equal(5, ex.RetryAfterSeconds);
    }

    private static (TelegramService Service, FakeTelegramApiClient Client, FakeRealtimeNotifier Notifier) BuildService(TestDb db)
    {
        var client = new FakeTelegramApiClient();
        var notifier = new FakeRealtimeNotifier();
        var notifications = new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance);
        var service = new TelegramService(
            db.Context,
            client,
            new FakeTokenCipher(),
            new ConfigurationBuilder().Build(),
            notifier,
            notifications,
            NullLogger<TelegramService>.Instance);
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

    private static TelegramAccount SeedAccount(TestDb db, string companyId)
    {
        var account = new TelegramAccount
        {
            CompanyId = companyId,
            BotUsername = "acme_bot",
            EncryptedBotToken = new FakeTokenCipher().Encrypt("bot-token"),
            IsActive = true,
        };
        db.Context.TelegramAccounts.Add(account);
        db.Context.SaveChanges();
        return account;
    }

    private static TelegramChatLink SeedChatLink(TestDb db, string companyId, string contactId, long chatId)
    {
        var link = new TelegramChatLink { CompanyId = companyId, ContactId = contactId, ChatId = chatId };
        db.Context.TelegramChatLinks.Add(link);
        db.Context.SaveChanges();
        return link;
    }
}
