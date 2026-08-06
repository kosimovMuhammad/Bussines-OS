using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class User : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public Role Role { get; set; } = Role.Employee;
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime? LastLoginAt { get; set; }

    public string? InviteTokenHash { get; set; }
    public DateTime? InviteTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Contact> OwnedContacts { get; set; } = new List<Contact>();
    public ICollection<Deal> OwnedDeals { get; set; } = new List<Deal>();
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public ICollection<OAuthConnection> OAuthConnections { get; set; } = new List<OAuthConnection>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<FileAsset> UploadedFiles { get; set; } = new List<FileAsset>();
    public ICollection<Meeting> OrganizedMeetings { get; set; } = new List<Meeting>();
    public CalendarSyncToken? CalendarSyncToken { get; set; }
}
