using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class CommunicationConfiguration : IEntityTypeConfiguration<Communication>
{
    public void Configure(EntityTypeBuilder<Communication> builder)
    {
        builder.ToTable("communications");

        builder.HasOne(cm => cm.Company)
            .WithMany(c => c.Communications)
            .HasForeignKey(cm => cm.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cm => cm.Contact)
            .WithMany(c => c.Communications)
            .HasForeignKey(cm => cm.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(cm => cm.AttachmentsJson).HasColumnName("attachments").HasColumnType("jsonb");

        builder.HasIndex(cm => cm.ProviderMessageId).IsUnique();
        builder.HasIndex(cm => new { cm.CompanyId, cm.ContactId });
        builder.HasIndex(cm => cm.Channel);
    }
}

public class AiSummaryConfiguration : IEntityTypeConfiguration<AiSummary>
{
    public void Configure(EntityTypeBuilder<AiSummary> builder)
    {
        builder.ToTable("ai_summaries");

        builder.HasOne(s => s.Communication)
            .WithOne(c => c.AiSummary)
            .HasForeignKey<AiSummary>(s => s.CommunicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.ActionItemsJson).HasColumnName("actionItems").HasColumnType("jsonb");
    }
}
