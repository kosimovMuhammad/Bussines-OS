using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasOne(u => u.Company)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.RbacRole)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RbacRoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(u => u.Manager)
            .WithMany(u => u.DirectReports)
            .HasForeignKey(u => u.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Restrict, not Cascade/SetNull - deleting a Department that still has active
        // employees must fail, not silently un-assign them (mirrors Contact->Deal).
        builder.HasOne(u => u.Department)
            .WithMany()
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Branch)
            .WithMany()
            .HasForeignKey(u => u.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(u => u.CompanyId);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.RbacRoleId);
        builder.HasIndex(u => u.ManagerId);
        builder.HasIndex(u => u.DepartmentId);
        builder.HasIndex(u => u.BranchId);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rt => rt.UserId);
    }
}

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasOne(k => k.Company)
            .WithMany()
            .HasForeignKey(k => k.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(k => k.CreatedByUser)
            .WithMany()
            .HasForeignKey(k => k.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(k => k.CompanyId);
        builder.HasIndex(k => k.KeyHash).IsUnique();
    }
}
