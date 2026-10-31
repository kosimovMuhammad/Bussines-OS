using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

// Covers the gap this pass closes: inbound messages on WhatsApp/Instagram/Telegram were
// already saved as Communication rows and pushed over SignalR (NotifyNewCommunicationAsync),
// but never raised a bell-icon Notification for the contact's owner. See
// WhatsAppService.ProcessIncomingMessageAsync, InstagramService.ProcessWebhookAsync,
// TelegramService.ProcessWebhookAsync.
public class InboundMessageNotificationTests
{
    [Fact]
    public async Task WhatsApp_IncomingMessage_OwnedContact_CreatesNewMessageNotification()
    {
        using var db = new TestDb();
        var (company, owner) = SeedCompanyAndUser(db);
        var contact = new Contact { CompanyId = company.Id, FirstName = "Jamshed", Phone = "+992900000001", OwnerId = owner.Id };
        db.Context.Contacts.Add(contact);
        db.Context.WhatsAppAccounts.Add(new WhatsAppAccount
        {
            CompanyId = company.Id,
            PhoneNumberId = "phone-number-id",
            WabaId = "waba-id",
            EncryptedAccessToken = new FakeTokenCipher().Encrypt("token"),
            IsActive = true,
        });
        db.Context.SaveChanges();

        var service = new WhatsAppService(
            db.Context, new FakeWhatsAppClient(), new FakeTokenCipher(), new FakeRealtimeNotifier(),
            new AuditLogService(db.Context),
            new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance),
            NullLogger<WhatsAppService>.Instance);

        await service.ProcessIncomingMessageAsync(new IncomingWhatsAppMessage("wamid.1", "phone-number-id", "+992900000001", "Salom, narx chand?", DateTime.UtcNow));

        var notification = Assert.Single(db.Context.Notifications);
        Assert.Equal(owner.Id, notification.UserId);
        Assert.Equal(NotificationType.NewMessage, notification.Type);
        Assert.Equal("Contact", notification.RelatedEntityType);
        Assert.Contains("Salom, narx chand?", notification.Body);
    }

    [Fact]
    public async Task WhatsApp_IncomingMessage_UnownedContact_CreatesNoNotification()
    {
        using var db = new TestDb();
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.WhatsAppAccounts.Add(new WhatsAppAccount
        {
            CompanyId = company.Id,
            PhoneNumberId = "phone-number-id",
            WabaId = "waba-id",
            EncryptedAccessToken = new FakeTokenCipher().Encrypt("token"),
            IsActive = true,
        });
        db.Context.SaveChanges();

        var service = new WhatsAppService(
            db.Context, new FakeWhatsAppClient(), new FakeTokenCipher(), new FakeRealtimeNotifier(),
            new AuditLogService(db.Context),
            new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance),
            NullLogger<WhatsAppService>.Instance);

        // Brand-new sender - WhatsAppService creates the Contact inline with no OwnerId.
        await service.ProcessIncomingMessageAsync(new IncomingWhatsAppMessage("wamid.1", "phone-number-id", "+992900000002", "Salom", DateTime.UtcNow));

        Assert.Single(db.Context.Communications);
        Assert.Empty(db.Context.Notifications);
    }

    [Fact]
    public async Task Instagram_Webhook_OwnedContact_CreatesNewMessageNotification()
    {
        using var db = new TestDb();
        var (company, owner) = SeedCompanyAndUser(db);
        var contact = new Contact { CompanyId = company.Id, FirstName = "Jamshed", OwnerId = owner.Id };
        db.Context.Contacts.Add(contact);
        db.Context.InstagramAccounts.Add(new InstagramAccount
        {
            CompanyId = company.Id,
            InstagramBusinessAccountId = "ig-business-1",
            EncryptedAccessToken = new FakeTokenCipher().Encrypt("token"),
            IsActive = true,
        });
        db.Context.InstagramConversationLinks.Add(new InstagramConversationLink
        {
            CompanyId = company.Id,
            ContactId = contact.Id,
            InstagramScopedId = "ig-scoped-1",
        });
        db.Context.SaveChanges();

        var service = new InstagramService(
            db.Context, new FakeInstagramApiClient(), new FakeTokenCipher(), new FakeOAuthStateService(), new FakeRealtimeNotifier(),
            new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance),
            NullLogger<InstagramService>.Instance);

        var payload = """
        {"object":"instagram","entry":[{"id":"ig-business-1","time":1700000000,"messaging":[
          {"sender":{"id":"ig-scoped-1"},"recipient":{"id":"ig-business-1"},"timestamp":1700000000000,"message":{"mid":"mid.1","text":"Salom, mahsulot mavjud?"}}
        ]}]}
        """;

        await service.ProcessWebhookAsync(payload);

        var notification = Assert.Single(db.Context.Notifications);
        Assert.Equal(owner.Id, notification.UserId);
        Assert.Equal(NotificationType.NewMessage, notification.Type);
        Assert.Contains("Salom, mahsulot mavjud?", notification.Body);
    }

    [Fact]
    public async Task Telegram_Webhook_OwnedContact_CreatesNewMessageNotification()
    {
        using var db = new TestDb();
        var (company, owner) = SeedCompanyAndUser(db);
        var contact = new Contact { CompanyId = company.Id, FirstName = "Jamshed", OwnerId = owner.Id };
        db.Context.Contacts.Add(contact);
        var account = new TelegramAccount
        {
            CompanyId = company.Id,
            BotUsername = "acme_bot",
            EncryptedBotToken = new FakeTokenCipher().Encrypt("bot-token"),
            IsActive = true,
        };
        db.Context.TelegramAccounts.Add(account);
        db.Context.TelegramChatLinks.Add(new TelegramChatLink
        {
            CompanyId = company.Id,
            ContactId = contact.Id,
            ChatId = 12345,
            TelegramUserId = 999,
        });
        db.Context.SaveChanges();

        var service = new TelegramService(
            db.Context, new FakeTelegramApiClient(), new FakeTokenCipher(), new ConfigurationBuilder().Build(), new FakeRealtimeNotifier(),
            new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance),
            NullLogger<TelegramService>.Instance);

        var payload = """
        {"update_id":1,"message":{"message_id":1,"date":1700000000,"chat":{"id":12345,"type":"private"},"from":{"id":999,"is_bot":false,"first_name":"Jamshed"},"text":"Salom, buyurtma bermoqchiman"}}
        """;

        await service.ProcessWebhookAsync(account.WebhookSecret, payload);

        var notification = Assert.Single(db.Context.Notifications);
        Assert.Equal(owner.Id, notification.UserId);
        Assert.Equal(NotificationType.NewMessage, notification.Type);
        Assert.Contains("Salom, buyurtma bermoqchiman", notification.Body);
    }

    [Fact]
    public async Task Telegram_Webhook_UnlinkedChat_AutoCreatesContactAndStoresMessage()
    {
        using var db = new TestDb();
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        var account = new TelegramAccount
        {
            CompanyId = company.Id,
            BotUsername = "acme_bot",
            EncryptedBotToken = new FakeTokenCipher().Encrypt("bot-token"),
            IsActive = true,
        };
        db.Context.TelegramAccounts.Add(account);
        db.Context.SaveChanges();

        var service = new TelegramService(
            db.Context, new FakeTelegramApiClient(), new FakeTokenCipher(), new ConfigurationBuilder().Build(), new FakeRealtimeNotifier(),
            new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance),
            NullLogger<TelegramService>.Instance);

        // No TelegramChatLink exists yet for this chat - nobody has shared their phone number,
        // so there is nothing to phone-match against.
        var payload = """
        {"update_id":1,"message":{"message_id":1,"date":1700000000,"chat":{"id":54321,"type":"private"},"from":{"id":777,"is_bot":false,"first_name":"Nilufar"},"text":"Salom, narxlar haqida"}}
        """;

        await service.ProcessWebhookAsync(account.WebhookSecret, payload);

        var communication = Assert.Single(db.Context.Communications);
        Assert.NotNull(communication.ContactId);
        Assert.Equal(CommunicationDirection.Inbound, communication.Direction);

        var contact = Assert.Single(db.Context.Contacts);
        Assert.Equal(communication.ContactId, contact.Id);
        Assert.Equal("Nilufar", contact.FirstName);

        var link = Assert.Single(db.Context.TelegramChatLinks);
        Assert.Equal(contact.Id, link.ContactId);
        Assert.Equal(TelegramLinkMethod.AutoCreated, link.LinkMethod);
    }

    [Fact]
    public async Task Instagram_Webhook_UnlinkedConversation_AutoCreatesContactAndStoresMessage()
    {
        using var db = new TestDb();
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.InstagramAccounts.Add(new InstagramAccount
        {
            CompanyId = company.Id,
            InstagramBusinessAccountId = "ig-business-1",
            EncryptedAccessToken = new FakeTokenCipher().Encrypt("token"),
            IsActive = true,
        });
        db.Context.SaveChanges();

        var service = new InstagramService(
            db.Context, new FakeInstagramApiClient(), new FakeTokenCipher(), new FakeOAuthStateService(), new FakeRealtimeNotifier(),
            new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance),
            NullLogger<InstagramService>.Instance);

        // No InstagramConversationLink exists yet for this sender.
        var payload = """
        {"object":"instagram","entry":[{"id":"ig-business-1","time":1700000000,"messaging":[
          {"sender":{"id":"ig-scoped-2"},"recipient":{"id":"ig-business-1"},"timestamp":1700000000000,"message":{"mid":"mid.2","text":"Salom, narxlar haqida"}}
        ]}]}
        """;

        await service.ProcessWebhookAsync(payload);

        var communication = Assert.Single(db.Context.Communications);
        Assert.NotNull(communication.ContactId);
        Assert.Equal(CommunicationDirection.Inbound, communication.Direction);

        var contact = Assert.Single(db.Context.Contacts);
        Assert.Equal(communication.ContactId, contact.Id);
        Assert.Equal("Instagram ig-scoped-2", contact.FirstName);

        var link = Assert.Single(db.Context.InstagramConversationLinks);
        Assert.Equal(contact.Id, link.ContactId);
        Assert.Equal(InstagramLinkMethod.AutoCreated, link.LinkMethod);
    }

    private static (Company Company, User Owner) SeedCompanyAndUser(TestDb db)
    {
        var company = new Company { Name = "Acme" };
        var owner = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(owner);
        db.Context.SaveChanges();
        return (company, owner);
    }
}
