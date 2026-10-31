using System.Net.Mail;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Email;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Services;

public class EmailService(
    IAppDbContext db,
    IGmailClient gmailClient,
    IOutlookClient outlookClient,
    IOAuthStateService oauthState,
    ITokenCipher tokenCipher,
    ILogger<EmailService> logger) : IEmailService
{
    public string GetGmailAuthUrl(string userId, string companyId) =>
        gmailClient.GetAuthUrl(oauthState.Create(userId, companyId));

    public async Task HandleGmailCallbackAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        var payload = oauthState.Validate(state) ?? throw new UnauthorizedAppException("Invalid or expired state");
        var tokens = await gmailClient.ExchangeCodeAsync(code, cancellationToken);
        await UpsertConnectionAsync(payload.CompanyId, payload.UserId, OAuthProvider.Google, tokens, cancellationToken);
    }

    public string GetOutlookAuthUrl(string userId, string companyId) =>
        outlookClient.GetAuthUrl(oauthState.Create(userId, companyId));

    public async Task HandleOutlookCallbackAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        var payload = oauthState.Validate(state) ?? throw new UnauthorizedAppException("Invalid or expired state");
        var tokens = await outlookClient.ExchangeCodeAsync(code, cancellationToken);
        await UpsertConnectionAsync(payload.CompanyId, payload.UserId, OAuthProvider.Microsoft, tokens, cancellationToken);
    }

    public async Task SendAsync(string companyId, string userId, SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        if (string.IsNullOrWhiteSpace(contact.Email))
        {
            throw new BadRequestException("Contact has no email address");
        }

        if (!IsWellFormedEmail(contact.Email))
        {
            throw new BadRequestException("Contact has an invalid email address");
        }

        // Company-scoped, not user-scoped: any user in the company should be able to send
        // through whichever Gmail/Outlook account the company connected, not only their own -
        // matches CompanyService.GetIntegrationsAsync's notion of "connected".
        var connection = await db.OAuthConnections
            .Where(c => c.CompanyId == companyId && (c.Provider == OAuthProvider.Google || c.Provider == OAuthProvider.Microsoft))
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BadRequestException("No email account connected. Connect Gmail/Outlook in Settings first");

        var accessToken = tokenCipher.Decrypt(connection.EncryptedAccessToken);
        var channel = connection.Provider == OAuthProvider.Google ? "Gmail" : "Outlook";

        var communication = new Communication
        {
            CompanyId = companyId,
            ContactId = contact.Id,
            Channel = CommunicationChannel.Email,
            Direction = CommunicationDirection.Outbound,
            Content = $"{request.Subject}\n\n{request.Body}",
            Status = CommunicationStatus.Queued,
        };

        try
        {
            var messageId = connection.Provider == OAuthProvider.Google
                ? await gmailClient.SendEmailAsync(accessToken, contact.Email, request.Subject, request.Body, cancellationToken)
                : await outlookClient.SendEmailAsync(accessToken, contact.Email, request.Subject, request.Body, cancellationToken);

            communication.Status = CommunicationStatus.Sent;
            communication.ProviderMessageId = messageId;
            logger.LogInformation(
                "{Channel} send succeeded: contactId={ContactId} to={To} sentBy={UserId} messageId={MessageId}",
                channel, contact.Id, MaskEmail(contact.Email), userId, messageId);
        }
        catch (Exception ex)
        {
            communication.Status = CommunicationStatus.Failed;
            db.Communications.Add(communication);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(ex, "{Channel} send failed: contactId={ContactId} to={To} sentBy={UserId}", channel, contact.Id, MaskEmail(contact.Email), userId);
            throw ex is ExternalServiceException ese
                ? ExternalSendErrorMapper.Map(channel, ese)
                : new ExternalServiceException($"Failed to send email: {ex.Message}");
        }

        db.Communications.Add(communication);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsWellFormedEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Same masking intent as WhatsAppService.MaskPhone: keep enough visible for debugging
    // without logging the full address (PII, Information-level logs aren't access-restricted).
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }

        return $"{email[..2]}***{email[at..]}";
    }

    private async Task UpsertConnectionAsync(string companyId, string userId, OAuthProvider provider, OAuthTokenResult tokens, CancellationToken cancellationToken)
    {
        var connection = await db.OAuthConnections
            .SingleOrDefaultAsync(c => c.UserId == userId && c.Provider == provider, cancellationToken);

        if (connection is null)
        {
            connection = new OAuthConnection { CompanyId = companyId, UserId = userId, Provider = provider };
            db.OAuthConnections.Add(connection);
        }

        connection.EncryptedAccessToken = tokenCipher.Encrypt(tokens.AccessToken);
        connection.EncryptedRefreshToken = tokens.RefreshToken is null ? connection.EncryptedRefreshToken : tokenCipher.Encrypt(tokens.RefreshToken);
        connection.ExpiresAt = tokens.ExpiresAt;

        await db.SaveChangesAsync(cancellationToken);
    }
}
