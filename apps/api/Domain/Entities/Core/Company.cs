using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class Company
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public Plan Plan { get; set; } = Plan.Starter;
    public string Timezone { get; set; } = "Asia/Dushanbe";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Communication> Communications { get; set; } = new List<Communication>();
    public ICollection<OAuthConnection> OAuthConnections { get; set; } = new List<OAuthConnection>();
    public ICollection<WhatsAppAccount> WaAccounts { get; set; } = new List<WhatsAppAccount>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<FileAsset> Files { get; set; } = new List<FileAsset>();
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
    public ICollection<AiQueryResult> AiQueryResults { get; set; } = new List<AiQueryResult>();
    public ICollection<AiEmailDraft> AiEmailDrafts { get; set; } = new List<AiEmailDraft>();
    public ICollection<AiQuotation> AiQuotations { get; set; } = new List<AiQuotation>();
    public ICollection<CalendarSyncToken> CalendarSyncTokens { get; set; } = new List<CalendarSyncToken>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
