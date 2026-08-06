using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");

        builder.HasOne(c => c.Company)
            .WithMany(co => co.Contacts)
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Owner)
            .WithMany(u => u.OwnedContacts)
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.CompanyId);
        builder.HasIndex(c => c.Phone);
        builder.HasIndex(c => c.Email);
    }
}

public class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("deals");

        builder.HasOne(d => d.Company)
            .WithMany(c => c.Deals)
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade - deleting a Contact must not silently wipe out its deals.
        builder.HasOne(d => d.Contact)
            .WithMany(c => c.Deals)
            .HasForeignKey(d => d.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Owner)
            .WithMany(u => u.OwnedDeals)
            .HasForeignKey(d => d.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(d => d.Value).HasColumnType("numeric");

        builder.HasIndex(d => d.CompanyId);
        builder.HasIndex(d => d.Stage);
    }
}

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");

        builder.HasOne(t => t.Company)
            .WithMany(c => c.Tasks)
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Assignee)
            .WithMany(u => u.AssignedTasks)
            .HasForeignKey(t => t.AssignedTo)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Deal)
            .WithMany(d => d.Tasks)
            .HasForeignKey(t => t.DealId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Contact)
            .WithMany(c => c.Tasks)
            .HasForeignKey(t => t.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.CompanyId);
        builder.HasIndex(t => t.AssignedTo);
    }
}

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("time_entries");

        builder.HasOne(te => te.Company)
            .WithMany(c => c.TimeEntries)
            .HasForeignKey(te => te.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(te => te.Task)
            .WithMany(t => t.TimeEntries)
            .HasForeignKey(te => te.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(te => te.User)
            .WithMany(u => u.TimeEntries)
            .HasForeignKey(te => te.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(te => te.TaskId);
        builder.HasIndex(te => te.UserId);
        builder.HasIndex(te => te.CompanyId);
    }
}
