using System.Data.Common;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Communications;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Services;

public class WhatsAppService(
    IAppDbContext db,
    IWhatsAppClient client,
    ITokenCipher tokenCipher,
    IRealtimeNotifier notifier,
    IAuditLogService auditLog,
    INotificationService notifications,
    ILogger<WhatsAppService> logger) : IWhatsAppService
{
    public async Task<IReadOnlyList<WhatsAppAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var accounts = await db.WhatsAppAccounts.Where(a => a.CompanyId == companyId).ToListAsync(cancellationToken);
        return accounts.Select(ToDto).ToList();
    }

    // WhatsApp Embedded Signup (mirrors InstagramService.HandleOAuthCallbackAsync): unlike the
    // Instagram flow this never leaves the current page (no browser redirect/state param - the
    // code comes straight from the frontend's FB.login() popup callback), so it's just a normal
    // authenticated endpoint, not an anonymous OAuth callback. The manual paste-a-token connect
    // path no longer exists: a user never types an id or token.
    //
    // NOTE (to be replaced in Phase 2): this still trusts the browser-reported ids; the reworked
    // flow derives them from the token via IWhatsAppClient.DebugTokenAsync / GetWabaPhoneNumbersAsync.
    public async Task<WhatsAppAccountDto> HandleEmbeddedSignupAsync(string companyId, HandleWhatsAppEmbeddedSignupRequest request, CancellationToken cancellationToken = default)
    {
        var shortLived = await client.ExchangeCodeAsync(request.Code, cancellationToken);
        var longLived = await client.ExchangeForLongLivedTokenAsync(shortLived.AccessToken, cancellationToken);
        var verifiedName = await client.GetVerifiedNameAsync(longLived.AccessToken, request.PhoneNumberId, cancellationToken);

        var account = await UpsertAccountAsync(
            companyId, request.PhoneNumberId, request.WabaId, longLived.AccessToken, verifiedName, longLived.ExpiresAt, cancellationToken);
        return ToDto(account);
    }

    // Hard delete (matches Instagram): removes the stored encrypted token and frees the globally
    // unique PhoneNumberId so the number can be connected again, by this or another company.
    // Contacts and Communications belong to the company, not to the connection, and nothing
    // references the account row, so message history is untouched.
    public async Task RemoveAccountAsync(string companyId, string actorUserId, string id, CancellationToken cancellationToken = default)
    {
        var account = await db.WhatsAppAccounts.SingleOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("WhatsApp account not found");

        // Best-effort: stop Meta from delivering this WABA's webhooks to us - but only when no
        // other connected number shares the WABA, otherwise we'd silence the sibling number too.
        // A failure here (token already revoked, Meta down, key rotated) must never block the
        // disconnect itself.
        var wabaStillInUse = await db.WhatsAppAccounts.AnyAsync(a => a.WabaId == account.WabaId && a.Id != account.Id, cancellationToken);
        if (!wabaStillInUse)
        {
            try
            {
                await client.UnsubscribeWebhookAsync(tokenCipher.Decrypt(account.EncryptedAccessToken), account.WabaId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "WhatsApp webhook unsubscribe failed for WABA {WabaId}; removing the account anyway", account.WabaId);
            }
        }

        db.WhatsAppAccounts.Remove(account);
        auditLog.Log(companyId, actorUserId, "whatsapp_account.disconnected", "WhatsAppAccount", id, new { account.PhoneNumberId, account.WabaId });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<WhatsAppAccount> UpsertAccountAsync(
        string companyId, string phoneNumberId, string wabaId, string accessToken, string? verifiedName, DateTime? tokenExpiresAt, CancellationToken cancellationToken)
    {
        var account = await db.WhatsAppAccounts
            .SingleOrDefaultAsync(a => a.CompanyId == companyId && a.PhoneNumberId == phoneNumberId, cancellationToken);

        if (account is null)
        {
            account = new WhatsAppAccount { CompanyId = companyId, PhoneNumberId = phoneNumberId };
            db.WhatsAppAccounts.Add(account);
        }

        account.WabaId = wabaId;
        account.EncryptedAccessToken = tokenCipher.Encrypt(accessToken);
        account.VerifiedName = verifiedName ?? account.VerifiedName;
        account.TokenExpiresAt = tokenExpiresAt;
        account.IsActive = true;
        account.UpdatedAt = DateTime.UtcNow;

        try
        {
            await client.SubscribeWebhookAsync(accessToken, wabaId, cancellationToken);
        }
        catch (ExternalServiceException ex)
        {
            // Same reasoning as InstagramService.UpsertAccountAsync - don't let a Meta-side
            // subscription failure block saving an otherwise-valid account.
            logger.LogWarning(ex, "WhatsApp webhook subscription failed for WABA {WabaId}; account will still be saved", wabaId);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: "23505" })
        {
            // phoneNumberId is unique across all companies - a Meta phone_number_id can only
            // ever belong to one WhatsApp Business account, so this is a real conflict, not a bug.
            throw new ConflictException("This WhatsApp phone number is already connected to another account.");
        }

        return account;
    }

    public async Task SendAsync(string companyId, SendWhatsAppRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        if (string.IsNullOrWhiteSpace(contact.Phone))
        {
            throw new BadRequestException("Contact has no phone number");
        }

        var to = WhatsAppPhoneNumberNormalizer.NormalizeForWhatsApp(contact.Phone);

        var account = await db.WhatsAppAccounts.Where(a => a.CompanyId == companyId && a.IsActive).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BadRequestException("No WhatsApp account connected. Connect WhatsApp Business in Settings first");

        var accessToken = tokenCipher.Decrypt(account.EncryptedAccessToken);

        var communication = new Communication
        {
            CompanyId = companyId,
            ContactId = contact.Id,
            Channel = CommunicationChannel.Whatsapp,
            Direction = CommunicationDirection.Outbound,
            Content = request.Message,
            Status = CommunicationStatus.Queued,
        };

        try
        {
            var messageId = await client.SendTextMessageAsync(account.PhoneNumberId, accessToken, to, request.Message, cancellationToken);
            communication.Status = CommunicationStatus.Sent;
            communication.ProviderMessageId = messageId;
            logger.LogInformation(
                "WhatsApp send succeeded: contactId={ContactId} to={To} messageId={MessageId}",
                contact.Id, MaskPhone(to), messageId);
        }
        catch (Exception ex)
        {
            communication.Status = CommunicationStatus.Failed;
            db.Communications.Add(communication);
            await db.SaveChangesAsync(cancellationToken);
            await notifier.NotifyNewCommunicationAsync(companyId, contact.BranchId, CommunicationDtoMapper.ToDto(communication, contact), cancellationToken);
            logger.LogInformation(ex, "WhatsApp send failed: contactId={ContactId} to={To}", contact.Id, MaskPhone(to));
            throw ex is ExternalServiceException ese
                ? ExternalSendErrorMapper.Map("WhatsApp", ese)
                : new ExternalServiceException($"Failed to send WhatsApp message: {ex.Message}");
        }

        db.Communications.Add(communication);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyNewCommunicationAsync(companyId, contact.BranchId, CommunicationDtoMapper.ToDto(communication, contact), cancellationToken);
    }

    public async Task ProcessIncomingMessageAsync(IncomingWhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        var alreadyProcessed = await db.Communications.AnyAsync(c => c.ProviderMessageId == message.ProviderMessageId, cancellationToken);
        if (alreadyProcessed)
        {
            return;
        }

        var account = await db.WhatsAppAccounts.SingleOrDefaultAsync(a => a.PhoneNumberId == message.PhoneNumberId, cancellationToken);
        if (account is null)
        {
            return;
        }

        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.CompanyId == account.CompanyId && c.Phone == message.From, cancellationToken);
        if (contact is null)
        {
            contact = new Contact { CompanyId = account.CompanyId, FirstName = message.From, Phone = message.From };
            db.Contacts.Add(contact);
        }

        var communication = new Communication
        {
            CompanyId = account.CompanyId,
            ContactId = contact.Id,
            Channel = CommunicationChannel.Whatsapp,
            Direction = CommunicationDirection.Inbound,
            Content = message.Text,
            ProviderMessageId = message.ProviderMessageId,
            Status = CommunicationStatus.Received,
            Timestamp = message.Timestamp,
        };
        db.Communications.Add(communication);

        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyNewCommunicationAsync(account.CompanyId, contact.BranchId, CommunicationDtoMapper.ToDto(communication, contact), cancellationToken);

        if (contact.OwnerId is not null)
        {
            var contactName = $"{contact.FirstName} {contact.LastName}".Trim();
            await notifications.NotifyAsync(account.CompanyId, contact.OwnerId, NotificationType.NewMessage,
                "New WhatsApp message", $"{contactName}: {InboundMessagePreview.Truncate(message.Text)}", "Contact", contact.Id, cancellationToken);
        }
    }

    public async Task<WhatsAppSendResult> SendPlatformTextMessageAsync(string actorUserId, SendPlatformWhatsAppTextRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlatformAdminAsync(actorUserId, cancellationToken);
        var to = WhatsAppPhoneNumberNormalizer.NormalizeForWhatsApp(request.To);

        try
        {
            var messageId = await client.SendPlatformTextMessageAsync(to, request.Message, cancellationToken);
            logger.LogInformation("Platform WhatsApp text send succeeded: to={To} messageId={MessageId}", MaskPhone(to), messageId);
            return new WhatsAppSendResult(true, messageId);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Platform WhatsApp text send failed: to={To}", MaskPhone(to));
            throw ex is ExternalServiceException ese
                ? ExternalSendErrorMapper.Map("WhatsApp", ese)
                : new ExternalServiceException($"Failed to send WhatsApp message: {ex.Message}");
        }
    }

    public async Task<WhatsAppSendResult> SendPlatformTemplateMessageAsync(string actorUserId, SendPlatformWhatsAppTemplateRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlatformAdminAsync(actorUserId, cancellationToken);
        var to = WhatsAppPhoneNumberNormalizer.NormalizeForWhatsApp(request.To);

        try
        {
            var messageId = await client.SendPlatformTemplateMessageAsync(to, request.TemplateName, request.LanguageCode, cancellationToken);
            logger.LogInformation("Platform WhatsApp template send succeeded: to={To} template={TemplateName} messageId={MessageId}", MaskPhone(to), request.TemplateName, messageId);
            return new WhatsAppSendResult(true, messageId);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Platform WhatsApp template send failed: to={To} template={TemplateName}", MaskPhone(to), request.TemplateName);
            throw ex is ExternalServiceException ese
                ? ExternalSendErrorMapper.Map("WhatsApp", ese)
                : new ExternalServiceException($"Failed to send WhatsApp message: {ex.Message}");
        }
    }

    // Keeps the country code and last 2 digits visible for debugging, masks the rest - phone
    // numbers are PII and Information-level logs are not access-restricted like the audit log.
    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length <= 5)
        {
            return "***";
        }

        return $"{phone[..3]}***{phone[^2..]}";
    }

    // Looked up fresh from the DB rather than carried on the JWT/ICurrentUserContext - same
    // reasoning and pattern as SubscriptionPlanService.EnsurePlatformAdminAsync. Platform-mode
    // sends use the platform's own WhatsApp number, not a tenant's, so tenant RBAC roles
    // (even Owner/Admin) are not sufficient on their own.
    private async Task EnsurePlatformAdminAsync(string actorUserId, CancellationToken cancellationToken)
    {
        var isPlatformAdmin = await db.Users.Where(u => u.Id == actorUserId).Select(u => u.IsPlatformAdmin).SingleOrDefaultAsync(cancellationToken);
        if (!isPlatformAdmin)
        {
            throw new ForbiddenException("Only platform admins can send platform-mode WhatsApp messages");
        }
    }

    private static WhatsAppAccountDto ToDto(WhatsAppAccount a) => new(
        a.Id, a.CompanyId, a.PhoneNumberId, a.WabaId, a.VerifiedName, a.DisplayPhoneNumber, a.IsActive, a.IsOnBizApp,
        a.ConnectionStatus, a.HistorySyncStatus, a.OnboardedAt, a.TokenExpiresAt, a.CreatedAt);
}
