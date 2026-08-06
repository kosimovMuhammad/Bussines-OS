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
