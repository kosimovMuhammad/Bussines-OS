using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class WhatsAppService(IAppDbContext db, IWhatsAppClient client, ITokenCipher tokenCipher) : IWhatsAppService
{
    public async Task<IReadOnlyList<WhatsAppAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var accounts = await db.WhatsAppAccounts.Where(a => a.CompanyId == companyId).ToListAsync(cancellationToken);
        return accounts.Select(ToDto).ToList();
    }

    public async Task<WhatsAppAccountDto> ConnectAccountAsync(string companyId, ConnectWhatsAppAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await db.WhatsAppAccounts
            .SingleOrDefaultAsync(a => a.CompanyId == companyId && a.PhoneNumberId == request.PhoneNumberId, cancellationToken);

        if (account is null)
        {
            account = new WhatsAppAccount { CompanyId = companyId, PhoneNumberId = request.PhoneNumberId };
            db.WhatsAppAccounts.Add(account);
        }

        account.WabaId = request.WabaId;
        account.EncryptedAccessToken = tokenCipher.Encrypt(request.AccessToken);
        account.VerifiedName = request.VerifiedName;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(account);
    }

    public async Task SendAsync(string companyId, SendWhatsAppRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        if (string.IsNullOrWhiteSpace(contact.Phone))
        {
            throw new ConflictException("Contact has no phone number");
        }

        var account = await db.WhatsAppAccounts.Where(a => a.CompanyId == companyId).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("No WhatsApp account connected");

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
            await client.SendTextMessageAsync(account.PhoneNumberId, accessToken, contact.Phone, request.Message, cancellationToken);
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

        db.Communications.Add(new Communication
        {
            CompanyId = account.CompanyId,
            ContactId = contact.Id,
            Channel = CommunicationChannel.Whatsapp,
            Direction = CommunicationDirection.Inbound,
            Content = message.Text,
            ProviderMessageId = message.ProviderMessageId,
            Status = CommunicationStatus.Received,
            Timestamp = message.Timestamp,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static WhatsAppAccountDto ToDto(WhatsAppAccount a) => new(a.Id, a.CompanyId, a.PhoneNumberId, a.WabaId, a.VerifiedName, a.CreatedAt);
}
