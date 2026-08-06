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
