using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class AiQueryResultConfiguration : IEntityTypeConfiguration<AiQueryResult>
{
    public void Configure(EntityTypeBuilder<AiQueryResult> builder)
    {
        builder.ToTable("ai_query_results");

        builder.HasOne(r => r.Company)
            .WithMany(c => c.AiQueryResults)
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.ResultRowsJson).HasColumnName("resultRows").HasColumnType("jsonb");

        builder.HasIndex(r => r.CompanyId);
    }
}

public class AiEmailDraftConfiguration : IEntityTypeConfiguration<AiEmailDraft>
{
    public void Configure(EntityTypeBuilder<AiEmailDraft> builder)
    {
        builder.ToTable("ai_email_drafts");

        builder.HasOne(d => d.Company)
            .WithMany(c => c.AiEmailDrafts)
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Contact)
            .WithMany(c => c.AiEmailDrafts)
            .HasForeignKey(d => d.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.CompanyId);
        builder.HasIndex(d => d.ContactId);
    }
}

public class AiQuotationConfiguration : IEntityTypeConfiguration<AiQuotation>
{
    public void Configure(EntityTypeBuilder<AiQuotation> builder)
    {
        builder.ToTable("ai_quotations");

        builder.HasOne(q => q.Company)
            .WithMany(c => c.AiQuotations)
            .HasForeignKey(q => q.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.Deal)
            .WithMany(d => d.AiQuotations)
            .HasForeignKey(q => q.DealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.FileAsset)
            .WithOne(f => f.AiQuotation)
            .HasForeignKey<AiQuotation>(q => q.FileAssetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(q => q.ItemsJson).HasColumnName("items").HasColumnType("jsonb");
        builder.Property(q => q.TotalAmount).HasColumnType("numeric");

        builder.HasIndex(q => q.CompanyId);
        builder.HasIndex(q => q.DealId);
    }
}
