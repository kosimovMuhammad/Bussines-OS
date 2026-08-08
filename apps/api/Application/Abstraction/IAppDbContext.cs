using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BusinessOS.Application.Abstraction;

public interface IAppDbContext
{
    // Needed for Search's raw trigram SQL (Database.SqlQuery<T>) - EF's LINQ tenant query
    // filter does NOT apply to raw SQL, so every raw query built against this must include
    // an explicit "companyId" predicate itself. See SearchService.
    DatabaseFacade Database { get; }

    // Generic escape hatch for ArchiveService (Phase 6) - Archive/Restore is implemented
    // once against IArchivable rather than per-entity, so it needs DbContext's own
    // Set<TEntity>() rather than one of the named properties below. DataContext (the only
    // implementation) already has this via its DbContext base class - nothing to add there.
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    DbSet<Company> Companies { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<Deal> Deals { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<TimeEntry> TimeEntries { get; }
    DbSet<Communication> Communications { get; }
    DbSet<AiSummary> AiSummaries { get; }
    DbSet<OAuthConnection> OAuthConnections { get; }
    DbSet<WhatsAppAccount> WhatsAppAccounts { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Payment> Payments { get; }
    DbSet<FileAsset> FileAssets { get; }
    DbSet<Meeting> Meetings { get; }
    DbSet<AiQueryResult> AiQueryResults { get; }
    DbSet<AiEmailDraft> AiEmailDrafts { get; }
    DbSet<AiQuotation> AiQuotations { get; }
    DbSet<CalendarSyncToken> CalendarSyncTokens { get; }
    DbSet<RbacRole> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserPermissionOverride> UserPermissionOverrides { get; }
    DbSet<ActivityLog> ActivityLogs { get; }
    DbSet<LoginHistory> LoginHistories { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<Department> Departments { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamMember> TeamMembers { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Workflow> Workflows { get; }
    DbSet<WorkflowTrigger> WorkflowTriggers { get; }
    DbSet<WorkflowConditionGroup> WorkflowConditionGroups { get; }
    DbSet<WorkflowCondition> WorkflowConditions { get; }
    DbSet<WorkflowAction> WorkflowActions { get; }
    DbSet<WorkflowExecution> WorkflowExecutions { get; }
    DbSet<WorkflowExecutionStep> WorkflowExecutionSteps { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<SubscriptionPlanFeature> SubscriptionPlanFeatures { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<UsageCounter> UsageCounters { get; }
    DbSet<SubscriptionInvoice> SubscriptionInvoices { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<Report> Reports { get; }
    DbSet<CompanySettings> CompanySettings { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Inventory> Inventories { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLineItem> PurchaseOrderLineItems { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<Contract> Contracts { get; }
    DbSet<Template> Templates { get; }
    DbSet<FileVersion> FileVersions { get; }
    DbSet<Webhook> Webhooks { get; }
    DbSet<WebhookDeliveryLog> WebhookDeliveryLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
