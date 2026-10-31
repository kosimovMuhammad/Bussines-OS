using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Communications;
using BusinessOS.Application.ViewModels.Telegram;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Services;

public class TelegramService(
    IAppDbContext db,
    ITelegramApiClient client,
    ITokenCipher tokenCipher,
    IConfiguration configuration,
    IRealtimeNotifier notifier,
    INotificationService notifications,
    ILogger<TelegramService> logger) : ITelegramService
{
    private const string WelcomeMessage = "Салом! Барои робита бо мо, лутфан рақами телефони худро мубодила кунед.";
    private const string ContactButtonText = "Ба мо рақами худро фиристед";

    public async Task<IReadOnlyList<TelegramAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var accounts = await db.TelegramAccounts.Where(a => a.CompanyId == companyId).ToListAsync(cancellationToken);
        return accounts.Select(ToDto).ToList();
    }

    public async Task<TelegramAccountDto> ConnectAccountAsync(string companyId, ConnectTelegramAccountRequest request, CancellationToken cancellationToken = default)
    {
        // Verifies the token is real before we ever persist it - GetMeAsync throws
        // ExternalServiceException (via TelegramApiClient) if Telegram rejects it.
        var botInfo = await client.GetMeAsync(request.BotToken, cancellationToken);
        var botUsername = botInfo.Username;
        if (string.IsNullOrWhiteSpace(botUsername))
        {
            throw new ExternalServiceException("This Telegram bot has no username configured");
        }

        var account = await db.TelegramAccounts
            .SingleOrDefaultAsync(a => a.CompanyId == companyId && a.BotUsername == botUsername, cancellationToken);

        if (account is null)
        {
            account = new TelegramAccount { CompanyId = companyId, BotUsername = botUsername };
            db.TelegramAccounts.Add(account);
        }

        account.EncryptedBotToken = tokenCipher.Encrypt(request.BotToken);
        account.IsActive = true;
        account.UpdatedAt = DateTime.UtcNow;

        var publicUrl = configuration["PUBLIC_URL"]
            ?? throw new InvalidOperationException("PUBLIC_URL is not configured (set it via user secrets or an environment variable)");
        var webhookUrl = $"{publicUrl.TrimEnd('/')}/webhooks/telegram/{account.WebhookSecret}";
        // SetWebhookAsync throws ExternalServiceException (mapped to a 502 by
        // ExceptionHandlingMiddleware) if Telegram rejects the registration, so a failure here
        // surfaces to the caller instead of silently leaving the bot without a working webhook.
        await client.SetWebhookAsync(request.BotToken, webhookUrl, account.WebhookSecret, cancellationToken);
        logger.LogInformation("Telegram webhook registered for bot {BotUsername} at {WebhookUrl}", botUsername, webhookUrl);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        {
            // botUsername is unique across all companies - a Telegram bot username can only
            // ever belong to one bot, so this is a real conflict, not a bug.
            throw new ConflictException("This Telegram bot is already connected to another account.");
        }

        return ToDto(account);
    }

    public async Task SendAsync(string companyId, SendTelegramMessageRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        var link = await db.TelegramChatLinks.SingleOrDefaultAsync(l => l.CompanyId == companyId && l.ContactId == contact.Id, cancellationToken)
            ?? throw new BadRequestException("This contact has not started a conversation with the Telegram bot yet");

        var account = await db.TelegramAccounts.Where(a => a.CompanyId == companyId && a.IsActive).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BadRequestException("No Telegram bot connected. Connect a Telegram bot in Settings first");

        var botToken = tokenCipher.Decrypt(account.EncryptedBotToken);

        var communication = new Communication
        {
            CompanyId = companyId,
            ContactId = contact.Id,
            Channel = CommunicationChannel.Telegram,
            Direction = CommunicationDirection.Outbound,
            Content = request.Message,
            Status = CommunicationStatus.Queued,
        };

        try
        {
            var messageId = await client.SendMessageAsync(botToken, link.ChatId, request.Message, cancellationToken);
            communication.Status = CommunicationStatus.Sent;
            communication.ProviderMessageId = messageId?.ToString();
            logger.LogInformation(
                "Telegram send succeeded: contactId={ContactId} chatId={ChatId} messageId={MessageId}",
                contact.Id, link.ChatId, messageId);
        }
        catch (Exception ex)
        {
            communication.Status = CommunicationStatus.Failed;
            db.Communications.Add(communication);
            await db.SaveChangesAsync(cancellationToken);
            await notifier.NotifyNewCommunicationAsync(companyId, contact.BranchId, CommunicationDtoMapper.ToDto(communication, contact), cancellationToken);
            logger.LogInformation(ex, "Telegram send failed: contactId={ContactId} chatId={ChatId}", contact.Id, link.ChatId);
            throw ex is ExternalServiceException ese
                ? ExternalSendErrorMapper.Map("Telegram", ese)
                : new ExternalServiceException($"Failed to send Telegram message: {ex.Message}");
        }

        db.Communications.Add(communication);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.NotifyNewCommunicationAsync(companyId, contact.BranchId, CommunicationDtoMapper.ToDto(communication, contact), cancellationToken);
    }

    public async Task ProcessWebhookAsync(string webhookSecret, string rawJson, CancellationToken cancellationToken = default)
    {
        var account = await db.TelegramAccounts.SingleOrDefaultAsync(a => a.WebhookSecret == webhookSecret && a.IsActive, cancellationToken);
        if (account is null)
        {
            logger.LogWarning("Telegram webhook: received update for unknown or inactive account (secret={Secret})", webhookSecret);
            return;
        }

        TelegramUpdate? update;
        try
        {
            update = JsonSerializer.Deserialize<TelegramUpdate>(rawJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Telegram webhook: failed to deserialize update for company {CompanyId}", account.CompanyId);
            return;
        }

        var message = update?.Message;
        if (message is null)
        {
            logger.LogInformation("Telegram webhook: received update {UpdateId} with no message; skipping", update?.UpdateId);
            return;
        }

        logger.LogInformation(
            "Telegram webhook: received message {MessageId} from chat {ChatId}", message.MessageId, message.Chat.Id);

        var link = await db.TelegramChatLinks.SingleOrDefaultAsync(
            l => l.CompanyId == account.CompanyId && l.ChatId == message.Chat.Id, cancellationToken);

        if (link is null)
        {
            link = new TelegramChatLink
            {
                CompanyId = account.CompanyId,
                ChatId = message.Chat.Id,
                TelegramUserId = message.From?.Id ?? 0,
            };
            db.TelegramChatLinks.Add(link);
        }

        // Refresh identity fields every message - Telegram usernames/names can change.
        link.TelegramUsername = message.From?.Username ?? link.TelegramUsername;
        link.TelegramFirstName = message.From?.FirstName ?? link.TelegramFirstName;
        link.TelegramLastName = message.From?.LastName ?? link.TelegramLastName;

        (Communication Communication, Contact Contact)? created = null;
        if (message.Contact is not null)
        {
            await HandleSharedContactAsync(account, link, message.Contact, cancellationToken);
        }
        else if (message.Text == "/start")
        {
            var botToken = tokenCipher.Decrypt(account.EncryptedBotToken);
            await client.SendMessageWithContactRequestAsync(botToken, message.Chat.Id, WelcomeMessage, ContactButtonText, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(message.Text))
        {
            created = await HandlePlainTextAsync(account, link, message, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (created is not null)
        {
            var (communication, contact) = created.Value;
            await notifier.NotifyNewCommunicationAsync(communication.CompanyId, contact.BranchId, CommunicationDtoMapper.ToDto(communication, contact), cancellationToken);

            if (contact.OwnerId is not null)
            {
                var contactName = $"{contact.FirstName} {contact.LastName}".Trim();
                await notifications.NotifyAsync(communication.CompanyId, contact.OwnerId, NotificationType.NewMessage,
                    "New Telegram message", $"{contactName}: {InboundMessagePreview.Truncate(communication.Content)}", "Contact", contact.Id, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<TelegramChatLinkDto>> ListChatLinksAsync(string companyId, bool? onlyUnlinked, CancellationToken cancellationToken = default)
    {
        var query = db.TelegramChatLinks.Where(l => l.CompanyId == companyId);
        if (onlyUnlinked == true)
        {
            query = query.Where(l => l.ContactId == null);
        }

        var links = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(cancellationToken);
        return links.Select(ToDto).ToList();
    }

    public async Task<TelegramChatLinkDto> LinkChatToContactAsync(string companyId, string chatLinkId, LinkTelegramChatToContactRequest request, CancellationToken cancellationToken = default)
    {
        var link = await db.TelegramChatLinks.SingleOrDefaultAsync(l => l.Id == chatLinkId && l.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Telegram chat link not found");

        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        link.ContactId = contact.Id;
        link.LinkMethod = TelegramLinkMethod.ManuallyLinked;
        link.LinkedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(link);
    }

    private async Task HandleSharedContactAsync(TelegramAccount account, TelegramChatLink link, TelegramContact sharedContact, CancellationToken cancellationToken)
    {
        var normalizedPhone = WhatsAppPhoneNumberNormalizer.Normalize(sharedContact.PhoneNumber);
        link.PhoneNumber = normalizedPhone;

        var candidates = await db.Contacts
            .Where(c => c.CompanyId == account.CompanyId && c.Phone != null)
            .ToListAsync(cancellationToken);

        var matchedContact = candidates.FirstOrDefault(c => WhatsAppPhoneNumberNormalizer.Normalize(c.Phone!) == normalizedPhone);

        if (matchedContact is not null)
        {
            link.ContactId = matchedContact.Id;
            link.LinkMethod = TelegramLinkMethod.AutoMatchedByPhone;
            link.LinkedAt = DateTime.UtcNow;
        }
    }

    private async Task<(Communication Communication, Contact Contact)?> HandlePlainTextAsync(TelegramAccount account, TelegramChatLink link, TelegramMessage message, CancellationToken cancellationToken)
    {
        var providerMessageId = message.MessageId.ToString();
        var alreadyProcessed = await db.Communications.AnyAsync(c => c.ProviderMessageId == providerMessageId, cancellationToken);
        if (alreadyProcessed)
        {
            return null;
        }

        Contact contact;
        if (link.ContactId is null)
        {
            // No phone-matched Contact exists for this chat - rather than dropping the message
            // (previously only link.LastMessagePreview was updated and it never reached
            // /communications), create a Contact so the conversation shows up immediately and
            // nothing is lost while waiting for a manual link.
            contact = CreateAutoContact(account.CompanyId, link);
            db.Contacts.Add(contact);
            link.ContactId = contact.Id;
            link.LinkMethod = TelegramLinkMethod.AutoCreated;
            link.LinkedAt = DateTime.UtcNow;
            logger.LogInformation(
                "Telegram webhook: auto-created contact {ContactId} for chat {ChatId}", contact.Id, link.ChatId);
        }
        else
        {
            var existingContact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == link.ContactId, cancellationToken);
            if (existingContact is null)
            {
                return null;
            }
            contact = existingContact;
        }

        var communication = new Communication
        {
            CompanyId = account.CompanyId,
            ContactId = link.ContactId,
            Channel = CommunicationChannel.Telegram,
            Direction = CommunicationDirection.Inbound,
            Content = message.Text ?? string.Empty,
            ProviderMessageId = providerMessageId,
            Status = CommunicationStatus.Received,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Date).UtcDateTime,
        };
        db.Communications.Add(communication);

        return (communication, contact);
    }

    private static Contact CreateAutoContact(string companyId, TelegramChatLink link)
    {
        var (firstName, lastName) = ResolveAutoContactName(link);
        return new Contact
        {
            CompanyId = companyId,
            FirstName = firstName,
            LastName = lastName,
            Phone = link.PhoneNumber,
        };
    }

    private static (string FirstName, string? LastName) ResolveAutoContactName(TelegramChatLink link)
    {
        if (!string.IsNullOrWhiteSpace(link.TelegramFirstName))
        {
            return (link.TelegramFirstName, link.TelegramLastName);
        }
        if (!string.IsNullOrWhiteSpace(link.TelegramUsername))
        {
            return ($"@{link.TelegramUsername}", null);
        }
        return ($"Telegram {link.ChatId}", null);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static TelegramAccountDto ToDto(TelegramAccount a) => new(a.Id, a.CompanyId, a.BotUsername, a.IsActive, a.CreatedAt);

    private static TelegramChatLinkDto ToDto(TelegramChatLink l) => new(
        l.Id, l.CompanyId, l.ContactId, l.ChatId, l.TelegramUserId, l.TelegramUsername,
        l.TelegramFirstName, l.TelegramLastName, l.PhoneNumber, l.LinkMethod, l.LinkedAt, l.LastMessagePreview, l.CreatedAt);
}
