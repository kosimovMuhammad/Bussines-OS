using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class OAuthConnectionConfiguration : IEntityTypeConfiguration<OAuthConnection>
{
    public void Configure(EntityTypeBuilder<OAuthConnection> builder)
    {
        builder.ToTable("oauth_connections");

        builder.HasOne(o => o.Company)
            .WithMany(c => c.OAuthConnections)
            .HasForeignKey(o => o.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.User)
            .WithMany(u => u.OAuthConnections)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.UserId, o.Provider }).IsUnique();
    }
}

public class WhatsAppAccountConfiguration : IEntityTypeConfiguration<WhatsAppAccount>
{
    public void Configure(EntityTypeBuilder<WhatsAppAccount> builder)
    {
        builder.ToTable("whatsapp_accounts");

        builder.HasOne(w => w.Company)
            .WithMany(c => c.WaAccounts)
            .HasForeignKey(w => w.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => w.PhoneNumberId).IsUnique();
    }
}

public class TelegramAccountConfiguration : IEntityTypeConfiguration<TelegramAccount>
{
    public void Configure(EntityTypeBuilder<TelegramAccount> builder)
    {
        builder.ToTable("telegram_accounts");

        builder.HasOne(t => t.Company)
            .WithMany(c => c.TelegramAccounts)
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.CompanyId);
        builder.HasIndex(t => t.WebhookSecret).IsUnique();

        // A Telegram bot username is globally unique on Telegram's side - same reasoning as
        // WhatsAppAccount.PhoneNumberId, so two companies can never both "own" the same bot.
        builder.HasIndex(t => t.BotUsername).IsUnique();
    }
}

public class TelegramChatLinkConfiguration : IEntityTypeConfiguration<TelegramChatLink>
{
    public void Configure(EntityTypeBuilder<TelegramChatLink> builder)
    {
        builder.ToTable("telegram_chat_links");

        builder.HasOne(t => t.Company)
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Cascade - unlinking/deleting a Contact should not destroy the chat's
        // Telegram identity/history, same reasoning as Webhook->WebhookDeliveryLog.
        builder.HasOne(t => t.Contact)
            .WithMany()
            .HasForeignKey(t => t.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.CompanyId, t.ChatId }).IsUnique();
        builder.HasIndex(t => t.ContactId);
    }
}

public class InstagramAccountConfiguration : IEntityTypeConfiguration<InstagramAccount>
{
    public void Configure(EntityTypeBuilder<InstagramAccount> builder)
    {
        builder.ToTable("instagram_accounts");

        builder.HasOne(i => i.Company)
            .WithMany(c => c.InstagramAccounts)
            .HasForeignKey(i => i.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.CompanyId);

        // An Instagram Business Account ID is globally unique on Meta's side - same reasoning
        // as WhatsAppAccount.PhoneNumberId and TelegramAccount.BotUsername, so two companies
        // can never both "own" the same Instagram account.
        builder.HasIndex(i => i.InstagramBusinessAccountId).IsUnique();
        builder.HasIndex(i => i.AppScopedUserId);
    }
}

public class InstagramConversationLinkConfiguration : IEntityTypeConfiguration<InstagramConversationLink>
{
    public void Configure(EntityTypeBuilder<InstagramConversationLink> builder)
    {
        builder.ToTable("instagram_conversation_links");

        builder.HasOne(i => i.Company)
            .WithMany()
            .HasForeignKey(i => i.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Cascade - unlinking/deleting a Contact should not destroy the
        // conversation's Instagram identity/history, same reasoning as TelegramChatLink.Contact.
        builder.HasOne(i => i.Contact)
            .WithMany()
            .HasForeignKey(i => i.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => new { i.CompanyId, i.InstagramScopedId }).IsUnique();
        builder.HasIndex(i => i.ContactId);
    }
}

public class CalendarSyncTokenConfiguration : IEntityTypeConfiguration<CalendarSyncToken>
{
    public void Configure(EntityTypeBuilder<CalendarSyncToken> builder)
    {
        builder.ToTable("calendar_sync_tokens");

        builder.HasOne(t => t.Company)
            .WithMany(c => c.CalendarSyncTokens)
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.User)
            .WithOne(u => u.CalendarSyncToken)
            .HasForeignKey<CalendarSyncToken>(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.ChannelId).IsUnique();
        builder.HasIndex(t => t.CompanyId);
    }
}

public class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> builder)
    {
        builder.ToTable("webhooks");

        builder.HasOne(w => w.Company)
            .WithMany()
            .HasForeignKey(w => w.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => w.CompanyId);
    }
}

public class WebhookDeliveryLogConfiguration : IEntityTypeConfiguration<WebhookDeliveryLog>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryLog> builder)
    {
        builder.ToTable("webhook_delivery_logs");

        builder.HasOne(l => l.Company)
            .WithMany()
            .HasForeignKey(l => l.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Cascade - a delivery log is the audit trail; deleting the Webhook
        // that produced it must not delete the history of what it sent (same reasoning as
        // PaymentTransaction->SubscriptionInvoice).
        builder.HasOne(l => l.Webhook)
            .WithMany()
            .HasForeignKey(l => l.WebhookId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => new { l.CompanyId, l.CreatedAt });
        builder.HasIndex(l => l.WebhookId);
    }
}
