using System.Reflection;
using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Data;

public class DataContext(DbContextOptions<DataContext> options, ICurrentTenantContext tenantContext)
    : DbContext(options), IAppDbContext
{
    private static readonly MethodInfo ApplyTenantFilterMethod = typeof(DataContext)
        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<Communication> Communications => Set<Communication>();
    public DbSet<AiSummary> AiSummaries => Set<AiSummary>();
    public DbSet<OAuthConnection> OAuthConnections => Set<OAuthConnection>();
    public DbSet<WhatsAppAccount> WhatsAppAccounts => Set<WhatsAppAccount>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<AiQueryResult> AiQueryResults => Set<AiQueryResult>();
    public DbSet<AiEmailDraft> AiEmailDrafts => Set<AiEmailDraft>();
    public DbSet<AiQuotation> AiQuotations => Set<AiQuotation>();
    public DbSet<CalendarSyncToken> CalendarSyncTokens => Set<CalendarSyncToken>();
    public DbSet<RbacRole> Roles => Set<RbacRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowTrigger> WorkflowTriggers => Set<WorkflowTrigger>();
    public DbSet<WorkflowConditionGroup> WorkflowConditionGroups => Set<WorkflowConditionGroup>();
    public DbSet<WorkflowCondition> WorkflowConditions => Set<WorkflowCondition>();
    public DbSet<WorkflowAction> WorkflowActions => Set<WorkflowAction>();
    public DbSet<WorkflowExecution> WorkflowExecutions => Set<WorkflowExecution>();
    public DbSet<WorkflowExecutionStep> WorkflowExecutionSteps => Set<WorkflowExecutionStep>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SubscriptionPlanFeature> SubscriptionPlanFeatures => Set<SubscriptionPlanFeature>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<UsageCounter> UsageCounters => Set<UsageCounter>();
    public DbSet<SubscriptionInvoice> SubscriptionInvoices => Set<SubscriptionInvoice>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLineItem> PurchaseOrderLineItems => Set<PurchaseOrderLineItem>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<FileVersion> FileVersions => Set<FileVersion>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Prisma quotes every identifier, so column names are exact camelCase (e.g. "companyId"),
        // not Postgres's usual snake_case. Default every property to that before entity configs
        // (which run next, and can override specific columns, e.g. the *Json-suffixed properties).
        // Enums store as their default int value - the live DB's enum columns are converted to
        // integer to match (see the InitialCreate migration's baseline notes).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToCamelCase(property.Name));
            }
        }

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    // Unscoped (tenantContext.CompanyId == null) when there's no active request tenant -
    // matches the Prisma middleware only activating inside an authenticated request.
    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => tenantContext.CompanyId == null || e.CompanyId == tenantContext.CompanyId);
    }

    private static string ToCamelCase(string pascalCase) =>
        string.IsNullOrEmpty(pascalCase) ? pascalCase : char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
}
