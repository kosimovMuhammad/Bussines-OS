using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class FileAssetConfiguration : IEntityTypeConfiguration<FileAsset>
{
    public void Configure(EntityTypeBuilder<FileAsset> builder)
    {
        builder.ToTable("file_assets");

        builder.HasOne(f => f.Company)
            .WithMany(c => c.Files)
            .HasForeignKey(f => f.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Contact)
            .WithMany(c => c.Files)
            .HasForeignKey(f => f.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(f => f.Deal)
            .WithMany(d => d.Files)
            .HasForeignKey(f => f.DealId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(f => f.Invoice)
            .WithMany(i => i.Files)
            .HasForeignKey(f => f.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(f => f.UploadedBy)
            .WithMany(u => u.UploadedFiles)
            .HasForeignKey(f => f.UploadedById)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.CompanyId);
        builder.HasIndex(f => f.ContactId);
        builder.HasIndex(f => f.DealId);
        builder.HasIndex(f => f.InvoiceId);
    }
}

public class FileVersionConfiguration : IEntityTypeConfiguration<FileVersion>
{
    public void Configure(EntityTypeBuilder<FileVersion> builder)
    {
        builder.ToTable("file_versions");

        builder.HasOne(v => v.Company)
            .WithMany()
            .HasForeignKey(v => v.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.FileAsset)
            .WithMany()
            .HasForeignKey(v => v.FileAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.UploadedBy)
            .WithMany()
            .HasForeignKey(v => v.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.FileAssetId, v.VersionNumber }).IsUnique();
    }
}
