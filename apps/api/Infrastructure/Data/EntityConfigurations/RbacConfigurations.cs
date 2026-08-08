using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class RbacRoleConfiguration : IEntityTypeConfiguration<RbacRole>
{
    public void Configure(EntityTypeBuilder<RbacRole> builder)
    {
        builder.ToTable("roles");

        builder.HasOne(r => r.Company)
            .WithMany(c => c.Roles)
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.CompanyId, r.Name }).IsUnique();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasIndex(p => p.Key).IsUnique();
        builder.HasIndex(p => p.Module);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // No inverse collection on Company - this FK exists purely for defense-in-depth
        // tenant scoping (matches TenantSaveChangesInterceptor's cross-tenant-mutation guard),
        // not for navigation.
        builder.HasOne(rp => rp.Company)
            .WithMany()
            .HasForeignKey(rp => rp.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rp => rp.CompanyId);
    }
}

public class UserPermissionOverrideConfiguration : IEntityTypeConfiguration<UserPermissionOverride>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverride> builder)
    {
        builder.ToTable("user_permission_overrides");

        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Permission)
            .WithMany()
            .HasForeignKey(o => o.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // No inverse collection on Company - defense-in-depth tenant scoping only,
        // same reasoning as RolePermission.CompanyId.
        builder.HasOne(o => o.Company)
            .WithMany()
            .HasForeignKey(o => o.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.UserId, o.PermissionId }).IsUnique();
        builder.HasIndex(o => o.CompanyId);
    }
}
