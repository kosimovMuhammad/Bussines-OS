using System.Data.Common;
using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Communications;
using BusinessOS.Application.ViewModels.Instagram;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Services;

public class InstagramService(
    IAppDbContext db,
    IInstagramApiClient client,
    ITokenCipher tokenCipher,
    IOAuthStateService oauthState,
    IRealtimeNotifier notifier,
    INotificationService notifications,
    ILogger<InstagramService> logger) : IInstagramService
{
    public async Task<IReadOnlyList<InstagramAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var accounts = await db.InstagramAccounts.Where(a => a.CompanyId == companyId).ToListAsync(cancellationToken);
        return accounts.Select(ToDto).ToList();
    }

    // Legacy manual-paste path - kept as a fallback (e.g. for a token obtained outside the
    // app's own OAuth flow), but GetAuthUrl/HandleOAuthCallbackAsync below is the flow the
    // frontend should actually drive: it never exposes a raw token to the user's clipboard.
    public async Task<InstagramAccountDto> ConnectAccountAsync(string companyId, ConnectInstagramAccountRequest request, CancellationToken cancellationToken = default)
    {
        var accountInfo = await client.GetAccountInfoAsync(request.AccessToken, cancellationToken);
        var account = await UpsertAccountAsync(companyId, accountInfo, request.AccessToken, null, cancellationToken);
        return ToDto(account);
    }

    public string GetAuthUrl(string userId, string companyId) =>
        client.GetAuthUrl(oauthState.Create(userId, companyId));

    // OAuth authorization-code flow (mirrors CalendarConnectionService.HandleCallbackAsync):
    // the user only ever authenticates on Instagram's own consent screen - no token is ever
    // typed or pasted into our app. The short-lived code from the callback is immediately
    // upgraded to a 60-day long-lived token so the connection survives without re-login;
    // InstagramTokenRefreshService keeps renewing it from then on.
    public async Task<InstagramAccountDto> HandleOAuthCallbackAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        var payload = oauthState.Validate(state) ?? throw new UnauthorizedAppException("Invalid or expired state");

        var shortLived = await client.ExchangeCodeAsync(code, cancellationToken);
        var longLived = await client.ExchangeForLongLivedTokenAsync(shortLived.AccessToken, cancellationToken);
        var accountInfo = await client.GetAccountInfoAsync(longLived.AccessToken, cancellationToken);

        var account = await UpsertAccountAsync(payload.CompanyId, accountInfo, longLived.AccessToken, longLived.ExpiresAt, cancellationToken);
        return ToDto(account);
    }

    private async Task<InstagramAccount> UpsertAccountAsync(
        string companyId, InstagramUserInfo accountInfo, string accessToken, DateTime? tokenExpiresAt, CancellationToken cancellationToken)
    {
        // Use UserId, not Id: Id is the token's app-scoped id, which does NOT match
        // messagingEvent.Recipient.Id in webhook payloads - UserId is the one Meta documents as
        // matching webhooks (see InstagramApiClient). Fall back to Id only if Meta ever omits
        // user_id for a given token, so a connect attempt never hard-fails on that alone.
        var igUserId = accountInfo.UserId ?? accountInfo.Id;

        var account = await db.InstagramAccounts
            .SingleOrDefaultAsync(a => a.CompanyId == companyId && a.InstagramBusinessAccountId == igUserId, cancellationToken);

        if (account is null)
        {
            account = new InstagramAccount { CompanyId = companyId, InstagramBusinessAccountId = igUserId };
            db.InstagramAccounts.Add(account);
        }

        account.Username = accountInfo.Username;
        account.EncryptedAccessToken = tokenCipher.Encrypt(accessToken);
        account.TokenExpiresAt = tokenExpiresAt;
        account.IsActive = true;
        account.UpdatedAt = DateTime.UtcNow;

        try
        {
            await client.SubscribeWebhookAsync(accessToken, igUserId, cancellationToken);
        }
        catch (ExternalServiceException ex)
        {
            // Webhook subscription is a separate concern from "is this a valid, connectable
            // account" - GetAccountInfoAsync above already proved that. Don't let a Meta-side
            // subscription failure (app's Instagram product/webhook fields not fully configured
            // in the App Dashboard yet, transient Meta error, etc.) block saving the account; the
            // caller can retry the subscription later once the underlying issue is fixed.
            logger.LogWarning(ex, "Instagram webhook subscription failed for account {AccountId}; account will still be saved", igUserId);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: "23505" })
        {
            // instagramBusinessAccountId is unique across all companies - an Instagram Business
            // Account can only ever belong to one company, so this is a real conflict, not a bug.
            throw new ConflictException("This Instagram account is already connected to another company.");
        }

        return account;
    }

    public async Task SendAsync(string companyId, SendInstagramMessageRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        var link = await db.InstagramConversationLinks
            .Where(l => l.CompanyId == companyId && l.ContactId == contact.Id)
            .OrderByDescending(l => l.LinkedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BadRequestException("This contact has not messaged your Instagram account yet - Instagram does not allow cold outreach");

        var account = await db.InstagramAccounts.Where(a => a.CompanyId == companyId && a.IsActive).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BadRequestException("No Instagram account connected. Connect Instagram in Settings first");

        var accessToken = tokenCipher.Decrypt(account.EncryptedAccessToken);

        var communication = new Communication
        {
            CompanyId = companyId,
            ContactId = contact.Id,
            Channel = CommunicationChannel.Instagram,
            Direction = CommunicationDirection.Outbound,
            Content = request.Message,
            Status = CommunicationStatus.Queued,
        };

        try
        {
            var messageId = await client.SendMessageAsync(accessToken, account.InstagramBusinessAccountId, link.InstagramScopedId, request.Message, cancellationToken);
            communication.Status = CommunicationStatus.Sent;
            communication.ProviderMessageId = messageId;
            logger.LogInformation(
                "Instagram send succeeded: contactId={ContactId} recipient={Recipient} messageId={MessageId}",
                contact.Id, link.InstagramScopedId, messageId);
        }
        catch (Exception ex)
        {
            communication.Status = CommunicationStatus.Failed;
            db.Communications.Add(communication);
            await db.SaveChangesAsync(cancellationToken);
            await notifier.NotifyNewCommunicationAsync(companyId, contact.BranchId, CommunicationDtoMapper.ToDto(communication, contact), cancellationToken);
            logger.LogInformation(ex, "Instagram send failed: contactId={ContactId} recipient={Recipient}", contact.Id, link.InstagramScopedId);
            throw ex is ExternalServiceException ese
                ? ExternalSendErrorMapper.Map("Instagram", ese)
                : new ExternalServiceException($"Failed to send Instagram message: {ex.Message}");
        }

        db.Communications.Add(communication);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyNewCommunicationAsync(companyId, contact.BranchId, CommunicationDtoMapper.ToDto(communication, contact), cancellationToken);
    }

    public async Task ProcessWebhookAsync(string rawJson, CancellationToken cancellationToken = default)
    {
        InstagramWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<InstagramWebhookPayload>(rawJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Instagram webhook payload failed to deserialize");
            return;
        }

        if (payload?.Entry is null)
        {
            logger.LogWarning("Instagram webhook payload had no entries: {Payload}", rawJson);
            return;
        }

        var eventCount = 0;
        var created = new List<(Communication Communication, Contact Contact)>();
        foreach (var entry in payload.Entry)
        {
            if (entry.Messaging is null)
            {
                continue;
            }

            foreach (var messagingEvent in entry.Messaging)
            {
                eventCount++;
                var result = await ProcessMessagingEventAsync(messagingEvent, cancellationToken);
                if (result is not null)
                {
                    created.Add(result.Value);
                }
            }
        }

        logger.LogInformation("Instagram webhook processed {EventCount} messaging event(s)", eventCount);
        await db.SaveChangesAsync(cancellationToken);

        // Notify only after the save succeeds - see IRealtimeNotifier for the group targeting.
        foreach (var (communication, contact) in created)
        {
            await notifier.NotifyNewCommunicationAsync(communication.CompanyId, contact.BranchId, CommunicationDtoMapper.ToDto(communication, contact), cancellationToken);

            if (contact.OwnerId is not null)
            {
                var contactName = $"{contact.FirstName} {contact.LastName}".Trim();
                await notifications.NotifyAsync(communication.CompanyId, contact.OwnerId, NotificationType.NewMessage,
                    "New Instagram message", $"{contactName}: {InboundMessagePreview.Truncate(communication.Content)}", "Contact", contact.Id, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<InstagramConversationLinkDto>> ListConversationLinksAsync(string companyId, bool? onlyUnlinked, CancellationToken cancellationToken = default)
    {
        var query = db.InstagramConversationLinks.Where(l => l.CompanyId == companyId);
        if (onlyUnlinked == true)
        {
            query = query.Where(l => l.ContactId == null);
        }

        var links = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(cancellationToken);
        return links.Select(ToDto).ToList();
    }

    public async Task<InstagramConversationLinkDto> LinkConversationToContactAsync(string companyId, string conversationLinkId, LinkInstagramConversationToContactRequest request, CancellationToken cancellationToken = default)
    {
        var link = await db.InstagramConversationLinks.SingleOrDefaultAsync(l => l.Id == conversationLinkId && l.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Instagram conversation link not found");

        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        link.ContactId = contact.Id;
        link.LinkMethod = InstagramLinkMethod.ManuallyLinked;
        link.LinkedAt = DateTime.UtcNow;

        // Messages that arrived while the conversation had no Contact only got stored as
        // LastMessagePreview (Communication.ContactId is required by the schema - see the field's
        // doc comment). Now that we have a Contact, turn that preview into real history instead of
        // silently dropping it from the conversation the moment it's linked.
        Communication? backfilled = null;
        if (!string.IsNullOrEmpty(link.LastMessagePreview))
        {
            backfilled = new Communication
            {
                CompanyId = companyId,
                ContactId = contact.Id,
                Channel = CommunicationChannel.Instagram,
                Direction = CommunicationDirection.Inbound,
                Content = link.LastMessagePreview,
                Status = CommunicationStatus.Received,
                Timestamp = link.LinkedAt.Value,
            };
            db.Communications.Add(backfilled);
            link.LastMessagePreview = null;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (backfilled is not null)
        {
            await notifier.NotifyNewCommunicationAsync(companyId, contact.BranchId, CommunicationDtoMapper.ToDto(backfilled, contact), cancellationToken);
        }

        return ToDto(link);
    }

    private async Task<(Communication Communication, Contact Contact)?> ProcessMessagingEventAsync(InstagramMessagingEvent messagingEvent, CancellationToken cancellationToken)
    {
        // recipient.id is our own Instagram Business Account - that's how we resolve which
        // tenant this webhook call belongs to (a single shared /webhooks/instagram endpoint
        // serves every company, unlike Telegram's per-account webhook secret).
        var account = await db.InstagramAccounts.SingleOrDefaultAsync(
            a => a.InstagramBusinessAccountId == messagingEvent.Recipient.Id && a.IsActive, cancellationToken);
        if (account is null)
        {
            logger.LogWarning(
                "Instagram webhook: no active InstagramAccount matches recipient.id {RecipientId}; dropping event",
                messagingEvent.Recipient.Id);
            return null;
        }

        var senderId = messagingEvent.Sender.Id;
        var link = await db.InstagramConversationLinks.SingleOrDefaultAsync(
            l => l.CompanyId == account.CompanyId && l.InstagramScopedId == senderId, cancellationToken);

        if (link is null)
        {
            link = new InstagramConversationLink { CompanyId = account.CompanyId, InstagramScopedId = senderId };
            db.InstagramConversationLinks.Add(link);
            logger.LogInformation(
                "Instagram webhook: created conversation link for company {CompanyId}, sender {SenderId}",
                account.CompanyId, senderId);
        }

        var message = messagingEvent.Message;
        if (message is null || string.IsNullOrEmpty(message.Text))
        {
            logger.LogInformation("Instagram webhook: messaging event for sender {SenderId} had no text; skipping", senderId);
            return null;
        }

        var alreadyProcessed = await db.Communications.AnyAsync(c => c.ProviderMessageId == message.Mid, cancellationToken);
        if (alreadyProcessed)
        {
            logger.LogInformation("Instagram webhook: message {Mid} already processed; skipping", message.Mid);
            return null;
        }

        Contact contact;
        if (link.ContactId is null)
        {
            // No Contact is linked to this conversation yet - rather than dropping the message
            // (previously only link.LastMessagePreview was updated and it never reached
            // /communications), create a Contact so the conversation shows up immediately and
            // nothing is lost while waiting for a manual link.
            contact = CreateAutoContact(account.CompanyId, link);
            db.Contacts.Add(contact);
            link.ContactId = contact.Id;
            link.LinkMethod = InstagramLinkMethod.AutoCreated;
            link.LinkedAt = DateTime.UtcNow;
            logger.LogInformation(
                "Instagram webhook: auto-created contact {ContactId} for sender {SenderId}", contact.Id, senderId);
        }
        else
        {
            var existingContact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == link.ContactId, cancellationToken);
            if (existingContact is null)
            {
                logger.LogWarning("Instagram webhook: link {LinkId} points at a missing contact {ContactId}; dropping event", link.Id, link.ContactId);
                return null;
            }
            contact = existingContact;
        }

        var communication = new Communication
        {
            CompanyId = account.CompanyId,
            ContactId = link.ContactId,
            Channel = CommunicationChannel.Instagram,
            Direction = CommunicationDirection.Inbound,
            Content = message.Text,
            ProviderMessageId = message.Mid,
            Status = CommunicationStatus.Received,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(messagingEvent.Timestamp).UtcDateTime,
        };
        db.Communications.Add(communication);
        logger.LogInformation(
            "Instagram webhook: saved inbound communication {Mid} for contact {ContactId}", message.Mid, link.ContactId);

        return (communication, contact);
    }

    private static Contact CreateAutoContact(string companyId, InstagramConversationLink link) => new()
    {
        CompanyId = companyId,
        FirstName = !string.IsNullOrWhiteSpace(link.InstagramUsername)
            ? $"@{link.InstagramUsername}"
            : $"Instagram {link.InstagramScopedId}",
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static InstagramAccountDto ToDto(InstagramAccount a) => new(
        a.Id, a.CompanyId, a.InstagramBusinessAccountId, a.Username, a.IsActive, a.TokenExpiresAt, a.CreatedAt);

    private static InstagramConversationLinkDto ToDto(InstagramConversationLink l) => new(
        l.Id, l.CompanyId, l.ContactId, l.InstagramScopedId, l.InstagramUsername, l.LinkMethod, l.LinkedAt, l.LastMessagePreview, l.CreatedAt);
}
