using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasOne(i => i.Company)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Deal)
            .WithMany(d => d.Invoices)
            .HasForeignKey(i => i.DealId)
            .OnDelete(DeleteBehavior.SetNull);

        // Restrict, not Cascade - deleting a Contact must not silently wipe out its invoices/payments.
        builder.HasOne(i => i.Contact)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.Amount).HasColumnType("numeric");

        builder.HasIndex(i => new { i.CompanyId, i.InvoiceNumber }).IsUnique();
        builder.HasIndex(i => i.CompanyId);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.ContactId);
        builder.HasIndex(i => i.DealId);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasOne(p => p.Company)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.Amount).HasColumnType("numeric");

        builder.HasIndex(p => p.InvoiceId);
        builder.HasIndex(p => p.CompanyId);
    }
}
