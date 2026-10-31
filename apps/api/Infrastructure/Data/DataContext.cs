using System.Reflection;
using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Data;

public class DataContext(DbContextOptions<DataContext> options, ICurrentTenantContext tenantContext)
    : DbContext(options), IAppDbContext
{
    private static readonly MethodInfo ApplyTenantFilterMethod = typeof(DataContext)
        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo ApplyBranchFilterMethod = typeof(DataContext)
        .GetMethod(nameof(ApplyBranchFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo ApplyContactBranchFilterMethod = typeof(DataContext)
        .GetMethod(nameof(ApplyContactBranchFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

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
    public DbSet<TelegramAccount> TelegramAccounts => Set<TelegramAccount>();
    public DbSet<TelegramChatLink> TelegramChatLinks => Set<TelegramChatLink>();
    public DbSet<InstagramAccount> InstagramAccounts => Set<InstagramAccount>();
    public DbSet<InstagramConversationLink> InstagramConversationLinks => Set<InstagramConversationLink>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<AiQueryResult> AiQueryResults => Set<AiQueryResult>();
    public DbSet<AiEmailDraft> AiEmailDrafts => Set<AiEmailDraft>();
    public DbSet<AiQuotation> AiQuotations => Set<AiQuotation>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AiAssistantConversation> AiAssistantConversations => Set<AiAssistantConversation>();
    public DbSet<AiAssistantMessage> AiAssistantMessages => Set<AiAssistantMessage>();
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
            // Order matters: IBranchScoped/IContactBranchScoped both extend ITenantScoped, and
            // EF Core only allows one HasQueryFilter per entity, so the more specific interface
            // must be checked first and must fold the tenant predicate into the same call.
            if (typeof(IBranchScoped).IsAssignableFrom(entityType.ClrType))
            {
                ApplyBranchFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
            else if (typeof(IContactBranchScoped).IsAssignableFrom(entityType.ClrType))
            {
                ApplyContactBranchFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
            else if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
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

    // Same tenant scoping as above, plus branch isolation: Role.Owner (the company's
    // director/super-admin - see the branch-scoping feature plan) sees every branch; everyone
    // else only sees rows for their own User.BranchId. A row with no BranchId assigned yet
    // stays visible to all branches so any branch admin can claim/link it.
    private void ApplyBranchFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IBranchScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e =>
                (tenantContext.CompanyId == null || e.CompanyId == tenantContext.CompanyId) &&
                (tenantContext.CompanyId == null || tenantContext.Role == Role.Owner ||
                 e.BranchId == null || e.BranchId == tenantContext.BranchId));
    }

    // Branch isolation for entities that hang off an optional Contact (Communication,
    // InstagramConversationLink, TelegramChatLink) instead of carrying their own BranchId -
    // branch is derived by joining to Contact.BranchId so it can never go stale if a contact's
    // branch is reassigned later. Unlinked rows (Contact == null) stay visible to all branches.
    private void ApplyContactBranchFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IContactBranchScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e =>
                (tenantContext.CompanyId == null || e.CompanyId == tenantContext.CompanyId) &&
                (tenantContext.CompanyId == null || tenantContext.Role == Role.Owner ||
                 e.Contact == null || e.Contact.BranchId == null || e.Contact.BranchId == tenantContext.BranchId));
    }

    private static string ToCamelCase(string pascalCase) =>
        string.IsNullOrEmpty(pascalCase) ? pascalCase : char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
}
