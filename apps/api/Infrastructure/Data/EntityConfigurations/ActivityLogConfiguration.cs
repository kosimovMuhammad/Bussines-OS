using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs");

        builder.HasOne(a => a.Company)
            .WithMany(c => c.ActivityLogs)
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // No inverse collection on User - ActivityLog is the highest-volume log table
        // and is always queried by EntityType/EntityId, never navigated from User.
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(a => a.ChangesJson).HasColumnName("changes").HasColumnType("jsonb");

        builder.HasIndex(a => new { a.CompanyId, a.EntityType, a.EntityId, a.CreatedAt });
        builder.HasIndex(a => new { a.CompanyId, a.CreatedAt });
    }
}
