using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.ToTable("login_histories");

        builder.HasOne(l => l.User)
            .WithMany(u => u.LoginHistories)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Not Cascade - a login record must survive even if the company row it
        // referenced is later removed; this is security/audit data, not tenant data.
        builder.HasOne(l => l.Company)
            .WithMany()
            .HasForeignKey(l => l.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => new { l.UserId, l.LoginAt });
        builder.HasIndex(l => new { l.CompanyId, l.LoginAt });
    }
}
