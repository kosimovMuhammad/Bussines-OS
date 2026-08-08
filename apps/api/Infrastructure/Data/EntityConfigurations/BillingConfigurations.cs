using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.EntityConfigurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");

        builder.Property(p => p.PriceMonthly).HasColumnType("numeric");
        builder.Property(p => p.PriceYearly).HasColumnType("numeric");

        builder.HasIndex(p => new { p.IsActive, p.SortOrder });
    }
}

public class SubscriptionPlanFeatureConfiguration : IEntityTypeConfiguration<SubscriptionPlanFeature>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlanFeature> builder)
    {
        builder.ToTable("subscription_plan_features");

        builder.HasOne(f => f.Plan)
            .WithMany(p => p.Features)
            .HasForeignKey(f => f.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.PlanId, f.FeatureKey }).IsUnique();
    }
}

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasOne(s => s.Company)
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade - an in-use plan can't be deleted out from under active
        // subscriptions (same reasoning as Contact->Deal). DELETE /plans pre-checks this.
        builder.HasOne(s => s.Plan)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, same reasoning as Plan above - a plan queued as someone's pending
        // downgrade target can't be deleted out from under them either.
        builder.HasOne(s => s.PendingPlan)
            .WithMany()
            .HasForeignKey(s => s.PendingPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.CompanyId).IsUnique();
        builder.HasIndex(s => s.PlanId);
    }
}

public class UsageCounterConfiguration : IEntityTypeConfiguration<UsageCounter>
{
    public void Configure(EntityTypeBuilder<UsageCounter> builder)
    {
        builder.ToTable("usage_counters");

        builder.HasOne(u => u.Company)
            .WithMany()
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => new { u.CompanyId, u.MetricKey }).IsUnique();
    }
}

public class SubscriptionInvoiceConfiguration : IEntityTypeConfiguration<SubscriptionInvoice>
{
    public void Configure(EntityTypeBuilder<SubscriptionInvoice> builder)
    {
        builder.ToTable("subscription_invoices");

        builder.HasOne(i => i.Company)
            .WithMany()
            .HasForeignKey(i => i.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Subscription)
            .WithMany(s => s.Invoices)
            .HasForeignKey(i => i.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(i => i.Amount).HasColumnType("numeric");

        builder.HasIndex(i => new { i.CompanyId, i.IssuedAt });
        builder.HasIndex(i => i.SubscriptionId);
    }
}

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");

        builder.HasOne(t => t.Company)
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Cascade - a payment record (the financial audit trail) must survive
        // even if its invoice link is removed.
        builder.HasOne(t => t.SubscriptionInvoice)
            .WithMany(i => i.PaymentTransactions)
            .HasForeignKey(t => t.SubscriptionInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(t => t.Amount).HasColumnType("numeric");

        builder.HasIndex(t => new { t.CompanyId, t.CreatedAt });

        // Partial unique index, not a plain one - ProviderTransactionId is null until the
        // provider assigns it, and multiple Pending transactions may exist without one yet.
        builder.HasIndex(t => t.ProviderTransactionId)
            .IsUnique()
            .HasFilter("\"providerTransactionId\" IS NOT NULL");
    }
}
