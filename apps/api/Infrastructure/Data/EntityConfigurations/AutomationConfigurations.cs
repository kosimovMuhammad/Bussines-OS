using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("workflows");

        builder.HasOne(w => w.Company)
            .WithMany()
            .HasForeignKey(w => w.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.CreatedByUser)
            .WithMany()
            .HasForeignKey(w => w.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(w => new { w.CompanyId, w.IsActive });
    }
}

public class WorkflowTriggerConfiguration : IEntityTypeConfiguration<WorkflowTrigger>
{
    public void Configure(EntityTypeBuilder<WorkflowTrigger> builder)
    {
        builder.ToTable("workflow_triggers");

        // 1:1 owned by Workflow - trigger is the dependent side, dies with its workflow.
        builder.HasOne(t => t.Workflow)
            .WithOne(w => w.Trigger)
            .HasForeignKey<WorkflowTrigger>(t => t.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Company)
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(t => t.ConfigJson).HasColumnName("config").HasColumnType("jsonb");

        builder.HasIndex(t => t.WorkflowId).IsUnique();
        builder.HasIndex(t => new { t.Type, t.NextRunAtUtc });
    }
}

public class WorkflowConditionGroupConfiguration : IEntityTypeConfiguration<WorkflowConditionGroup>
{
    public void Configure(EntityTypeBuilder<WorkflowConditionGroup> builder)
    {
        builder.ToTable("workflow_condition_groups");

        builder.HasOne(g => g.Workflow)
            .WithMany(w => w.ConditionGroups)
            .HasForeignKey(g => g.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing Cascade - a sub-group has no meaning without its parent.
        // Postgres allows this (unlike SQL Server's multiple-cascade-path restriction).
        builder.HasOne(g => g.ParentGroup)
            .WithMany(g => g.ChildGroups)
            .HasForeignKey(g => g.ParentGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.WorkflowId);
        builder.HasIndex(g => g.ParentGroupId);
    }
}

public class WorkflowConditionConfiguration : IEntityTypeConfiguration<WorkflowCondition>
{
    public void Configure(EntityTypeBuilder<WorkflowCondition> builder)
    {
        builder.ToTable("workflow_conditions");

        builder.HasOne(c => c.ConditionGroup)
            .WithMany(g => g.Conditions)
            .HasForeignKey(c => c.ConditionGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.ConditionGroupId);
    }
}

public class WorkflowActionConfiguration : IEntityTypeConfiguration<WorkflowAction>
{
    public void Configure(EntityTypeBuilder<WorkflowAction> builder)
    {
        builder.ToTable("workflow_actions");

        builder.HasOne(a => a.Workflow)
            .WithMany(w => w.Actions)
            .HasForeignKey(a => a.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.ConfigJson).HasColumnName("config").HasColumnType("jsonb");

        builder.HasIndex(a => new { a.WorkflowId, a.OrderIndex });
    }
}

public class WorkflowExecutionConfiguration : IEntityTypeConfiguration<WorkflowExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowExecution> builder)
    {
        builder.ToTable("workflow_executions");

        builder.HasOne(e => e.Workflow)
            .WithMany(w => w.Executions)
            .HasForeignKey(e => e.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.TriggerContextJson).HasColumnName("triggerContext").HasColumnType("jsonb");

        builder.HasIndex(e => new { e.WorkflowId, e.StartedAt });
        builder.HasIndex(e => new { e.CompanyId, e.StartedAt });
    }
}

public class WorkflowExecutionStepConfiguration : IEntityTypeConfiguration<WorkflowExecutionStep>
{
    public void Configure(EntityTypeBuilder<WorkflowExecutionStep> builder)
    {
        builder.ToTable("workflow_execution_steps");

        builder.HasOne(s => s.WorkflowExecution)
            .WithMany(e => e.Steps)
            .HasForeignKey(s => s.WorkflowExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Cascade/Restrict - editing or deleting a WorkflowAction later must
        // not corrupt or block deletion of the execution history it once produced.
        builder.HasOne(s => s.Action)
            .WithMany(a => a.ExecutionSteps)
            .HasForeignKey(s => s.ActionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(s => s.Output).HasColumnType("jsonb");
        builder.Property(s => s.Error).HasColumnType("jsonb");

        builder.HasIndex(s => s.WorkflowExecutionId);
    }
}
