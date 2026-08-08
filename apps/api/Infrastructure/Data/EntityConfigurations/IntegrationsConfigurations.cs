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
