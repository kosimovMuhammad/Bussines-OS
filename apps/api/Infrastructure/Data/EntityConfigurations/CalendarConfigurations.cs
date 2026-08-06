using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("meetings");

        builder.HasOne(m => m.Company)
            .WithMany(c => c.Meetings)
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Contact)
            .WithMany(c => c.Meetings)
            .HasForeignKey(m => m.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.Deal)
            .WithMany(d => d.Meetings)
            .HasForeignKey(m => m.DealId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.Organizer)
            .WithMany(u => u.OrganizedMeetings)
            .HasForeignKey(m => m.OrganizerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.GoogleEventId).IsUnique();
        builder.HasIndex(m => m.CompanyId);
        builder.HasIndex(m => m.StartAt);
        builder.HasIndex(m => m.ContactId);
        builder.HasIndex(m => m.DealId);
        builder.HasIndex(m => m.OrganizerId);
    }
}
