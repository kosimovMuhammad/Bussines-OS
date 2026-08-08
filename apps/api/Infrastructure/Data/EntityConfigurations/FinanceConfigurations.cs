using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");

        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not SetNull - PaidById is required (every expense needs a payer), and
        // EF Core doesn't allow SetNull on a non-nullable FK anyway. Same "can't delete out
        // from under referencing rows" reasoning as Contact->Deal.
        builder.HasOne(e => e.PaidBy)
            .WithMany()
            .HasForeignKey(e => e.PaidById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ReceiptFile)
            .WithMany()
            .HasForeignKey(e => e.ReceiptFileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(e => e.Amount).HasColumnType("numeric");

        builder.HasIndex(e => e.CompanyId);
        builder.HasIndex(e => e.Date);
        builder.HasIndex(e => e.PaidById);
    }
}

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts");

        builder.HasOne(c => c.Company)
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Contact)
            .WithMany()
            .HasForeignKey(c => c.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Deal)
            .WithMany()
            .HasForeignKey(c => c.DealId)
            .OnDelete(DeleteBehavior.SetNull);

        // SetNull - a Template is reusable boilerplate; deleting it must not touch
        // Contracts already generated from it (their Content is already resolved/copied).
        builder.HasOne(c => c.Template)
            .WithMany(t => t.Contracts)
            .HasForeignKey(c => c.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.SignedByUser)
            .WithMany()
            .HasForeignKey(c => c.SignedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.CompanyId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.ContactId);
        builder.HasIndex(c => c.DealId);
    }
}

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");

        builder.HasOne(t => t.Company)
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.CompanyId);
        builder.HasIndex(t => t.Type);
    }
}
