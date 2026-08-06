using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Abstraction;

public interface IAppDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
