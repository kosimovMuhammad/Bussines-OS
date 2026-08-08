using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");

        builder.HasOne(d => d.Company)
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade - deleting a department with sub-departments must fail,
        // not silently orphan them (the service blocks this before it ever reaches the DB).
        builder.HasOne(d => d.ParentDepartment)
            .WithMany(d => d.SubDepartments)
            .HasForeignKey(d => d.ParentDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Manager)
            .WithMany()
            .HasForeignKey(d => d.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.CompanyId);
        builder.HasIndex(d => d.ManagerId);

        // Name unique per company at the same tree level. A plain (CompanyId,
        // ParentDepartmentId, Name) index would let multiple NULL ParentDepartmentId rows
        // coexist under Postgres's null-distinct semantics, so top-level uniqueness needs
        // its own filtered index.
        builder.HasIndex(d => new { d.CompanyId, d.ParentDepartmentId, d.Name })
            .IsUnique()
            .HasFilter("\"parentDepartmentId\" IS NOT NULL");
        builder.HasIndex(d => new { d.CompanyId, d.Name })
            .IsUnique()
            .HasFilter("\"parentDepartmentId\" IS NULL");
    }
}

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");

        builder.HasOne(t => t.Company)
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Restrict - a team can exist without a parent department, so deleting
        // the department just detaches its teams instead of blocking.
        builder.HasOne(t => t.Department)
            .WithMany()
            .HasForeignKey(t => t.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.TeamLead)
            .WithMany()
            .HasForeignKey(t => t.TeamLeadId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.CompanyId);
        builder.HasIndex(t => t.DepartmentId);
    }
}

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("team_members");

        builder.HasKey(tm => new { tm.TeamId, tm.UserId });

        builder.HasOne(tm => tm.Team)
            .WithMany()
            .HasForeignKey(tm => tm.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tm => tm.User)
            .WithMany()
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // No inverse collection on Company - defense-in-depth tenant scoping only,
        // same reasoning as RolePermission.CompanyId.
        builder.HasOne(tm => tm.Company)
            .WithMany()
            .HasForeignKey(tm => tm.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tm => tm.CompanyId);
        builder.HasIndex(tm => tm.UserId);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");

        builder.HasOne(b => b.Company)
            .WithMany()
            .HasForeignKey(b => b.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.CompanyId);

        // At most one HQ per company (zero allowed). Service layer auto-unsets the previous
        // HQ when a new one is flagged; this partial unique index is the DB-level backstop.
        builder.HasIndex(b => new { b.CompanyId, b.IsHeadquarters })
            .IsUnique()
            .HasFilter("\"isHeadquarters\" = true");
    }
}
