using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Email;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class EmailServiceSendTests
{
    [Fact]
    public async Task SendAsync_HappyPath_MarksSentAndStoresProviderMessageId()
    {
        using var db = new TestDb();
        var (service, gmail, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, "client@example.com");
        var user = SeedUser(db, company.Id);
        SeedConnection(db, company.Id, user.Id, OAuthProvider.Google);

        gmail.NextMessageId = "gmail-1";

        await service.SendAsync(company.Id, user.Id, new SendEmailRequest(contact.Id, "Hi", "Body"));

        var communication = Assert.Single(db.Context.Communications);
        Assert.Equal(CommunicationStatus.Sent, communication.Status);
        Assert.Equal("gmail-1", communication.ProviderMessageId);
        Assert.Single(gmail.SentEmails);
    }

    [Fact]
    public async Task SendAsync_AnyCompanyUserConnection_IsUsedRegardlessOfSender()
    {
        // A different user connected the company's Gmail - the sender should still be able to
        // send through it (connection is company-scoped, not tied to whoever connected it).
        using var db = new TestDb();
        var (service, gmail, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, "client@example.com");
        var connector = SeedUser(db, company.Id);
        var sender = SeedUser(db, company.Id);
        SeedConnection(db, company.Id, connector.Id, OAuthProvider.Google);

        await service.SendAsync(company.Id, sender.Id, new SendEmailRequest(contact.Id, "Hi", "Body"));

        Assert.Single(gmail.SentEmails);
    }

    [Fact]
    public async Task SendAsync_ContactHasNoEmail_ThrowsBadRequest()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, email: null);
        var user = SeedUser(db, company.Id);
        SeedConnection(db, company.Id, user.Id, OAuthProvider.Google);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SendAsync(company.Id, user.Id, new SendEmailRequest(contact.Id, "Hi", "Body")));
    }

    [Fact]
    public async Task SendAsync_ContactHasMalformedEmail_ThrowsBadRequest()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, email: "not-an-email");
        var user = SeedUser(db, company.Id);
        SeedConnection(db, company.Id, user.Id, OAuthProvider.Google);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SendAsync(company.Id, user.Id, new SendEmailRequest(contact.Id, "Hi", "Body")));
    }

    [Fact]
    public async Task SendAsync_ContactBelongsToAnotherCompany_ThrowsNotFound()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var otherCompany = Seed(db);
        var contact = SeedContact(db, otherCompany.Id, "client@example.com");
        var user = SeedUser(db, company.Id);
        SeedConnection(db, company.Id, user.Id, OAuthProvider.Google);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SendAsync(company.Id, user.Id, new SendEmailRequest(contact.Id, "Hi", "Body")));
    }

    [Fact]
    public async Task SendAsync_NoEmailAccountConnected_ThrowsBadRequestWithActionableMessage()
    {
        using var db = new TestDb();
        var (service, _, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, "client@example.com");
        var user = SeedUser(db, company.Id);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SendAsync(company.Id, user.Id, new SendEmailRequest(contact.Id, "Hi", "Body")));

        Assert.Equal("No email account connected. Connect Gmail/Outlook in Settings first", ex.Message);
    }

    [Fact]
    public async Task SendAsync_ProviderAuthExpired_ThrowsUnauthorizedAndMarksFailed()
    {
        using var db = new TestDb();
        var (service, gmail, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, "client@example.com");
        var user = SeedUser(db, company.Id);
        SeedConnection(db, company.Id, user.Id, OAuthProvider.Google);

        gmail.NextSendException = new ExternalServiceException("invalid_grant") { ProviderStatusCode = 401 };

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            service.SendAsync(company.Id, user.Id, new SendEmailRequest(contact.Id, "Hi", "Body")));

        var communication = Assert.Single(db.Context.Communications);
        Assert.Equal(CommunicationStatus.Failed, communication.Status);
    }

    [Fact]
    public async Task SendAsync_ProviderRateLimited_ThrowsRateLimited()
    {
        using var db = new TestDb();
        var (service, gmail, _) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, "client@example.com");
        var user = SeedUser(db, company.Id);
        SeedConnection(db, company.Id, user.Id, OAuthProvider.Google);

        gmail.NextSendException = new ExternalServiceException("rate limited") { ProviderStatusCode = 429, RetryAfterSeconds = 15 };

        var ex = await Assert.ThrowsAsync<RateLimitedException>(() =>
            service.SendAsync(company.Id, user.Id, new SendEmailRequest(contact.Id, "Hi", "Body")));

        Assert.Equal(15, ex.RetryAfterSeconds);
    }

    [Fact]
    public async Task SendAsync_OutlookConnection_SendsViaOutlook()
    {
        using var db = new TestDb();
        var (service, gmail, outlook) = BuildService(db);
        var company = Seed(db);
        var contact = SeedContact(db, company.Id, "client@example.com");
        var user = SeedUser(db, company.Id);
        SeedConnection(db, company.Id, user.Id, OAuthProvider.Microsoft);

        await service.SendAsync(company.Id, user.Id, new SendEmailRequest(contact.Id, "Hi", "Body"));

        Assert.Empty(gmail.SentEmails);
        Assert.Single(outlook.SentEmails);
    }

    private static (EmailService Service, FakeGmailClient Gmail, FakeOutlookClient Outlook) BuildService(TestDb db)
    {
        var gmail = new FakeGmailClient();
        var outlook = new FakeOutlookClient();
        var service = new EmailService(
            db.Context,
            gmail,
            outlook,
            new FakeOAuthStateService(),
            new FakeTokenCipher(),
            NullLogger<EmailService>.Instance);
        return (service, gmail, outlook);
    }

    private static Company Seed(TestDb db)
    {
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        db.Context.SaveChanges();
        return company;
    }

    private static Contact SeedContact(TestDb db, string companyId, string? email)
    {
        var contact = new Contact { CompanyId = companyId, FirstName = "Jamshed", Email = email };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();
        return contact;
    }

    private static User SeedUser(TestDb db, string companyId)
    {
        var user = new User { CompanyId = companyId, Name = "Sender", Email = $"{Guid.NewGuid():N}@acme.tj" };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        return user;
    }

    private static OAuthConnection SeedConnection(TestDb db, string companyId, string userId, OAuthProvider provider)
    {
        var connection = new OAuthConnection
        {
            CompanyId = companyId,
            UserId = userId,
            Provider = provider,
            EncryptedAccessToken = new FakeTokenCipher().Encrypt("token"),
        };
        db.Context.OAuthConnections.Add(connection);
        db.Context.SaveChanges();
        return connection;
    }
}
