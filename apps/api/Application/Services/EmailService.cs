using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Email;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class EmailService(
    IAppDbContext db,
    IGmailClient gmailClient,
    IOutlookClient outlookClient,
    IOAuthStateService oauthState,
    ITokenCipher tokenCipher) : IEmailService
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
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        if (string.IsNullOrWhiteSpace(contact.Email))
        {
            throw new ConflictException("Contact has no email address");
        }

        var connection = await db.OAuthConnections
            .Where(c => c.UserId == userId && (c.Provider == OAuthProvider.Google || c.Provider == OAuthProvider.Microsoft))
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("No email provider connected");

        var accessToken = tokenCipher.Decrypt(connection.EncryptedAccessToken);

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
            if (connection.Provider == OAuthProvider.Google)
            {
                await gmailClient.SendEmailAsync(accessToken, contact.Email, request.Subject, request.Body, cancellationToken);
            }
            else
            {
                await outlookClient.SendEmailAsync(accessToken, contact.Email, request.Subject, request.Body, cancellationToken);
            }

            communication.Status = CommunicationStatus.Sent;
        }
        catch
        {
            communication.Status = CommunicationStatus.Failed;
            db.Communications.Add(communication);
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }

        db.Communications.Add(communication);
        await db.SaveChangesAsync(cancellationToken);
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
