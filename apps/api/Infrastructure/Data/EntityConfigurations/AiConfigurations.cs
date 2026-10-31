using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
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
        builder.Property(r => r.ResultType).HasDefaultValue(AiResultType.Text);

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

        builder.HasOne(q => q.RequestedByUser)
            .WithMany()
            .HasForeignKey(q => q.RequestedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

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

public class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("ai_usage_logs");

        builder.HasOne(l => l.Company)
            .WithMany(c => c.AiUsageLogs)
            .HasForeignKey(l => l.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.User)
            .WithMany(u => u.AiUsageLogs)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(l => l.Cost).HasColumnType("numeric");

        builder.HasIndex(l => new { l.CompanyId, l.CreatedAt });
        builder.HasIndex(l => l.UserId);
    }
}

public class AiAssistantConversationConfiguration : IEntityTypeConfiguration<AiAssistantConversation>
{
    public void Configure(EntityTypeBuilder<AiAssistantConversation> builder)
    {
        builder.ToTable("ai_assistant_conversations");

        builder.HasOne(c => c.Company)
            .WithMany(co => co.AiAssistantConversations)
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.User)
            .WithMany(u => u.AiAssistantConversations)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.CompanyId);
        builder.HasIndex(c => c.UserId);
    }
}

public class AiAssistantMessageConfiguration : IEntityTypeConfiguration<AiAssistantMessage>
{
    public void Configure(EntityTypeBuilder<AiAssistantMessage> builder)
    {
        builder.ToTable("ai_assistant_messages");

        builder.HasOne(m => m.Company)
            .WithMany()
            .HasForeignKey(m => m.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.ConversationId);
        builder.HasIndex(m => m.CompanyId);
    }
}
