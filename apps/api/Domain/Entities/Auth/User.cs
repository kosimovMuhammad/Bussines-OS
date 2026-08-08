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
    // Legacy coarse role, still the source of truth for authorization until the RBAC
    // cutover phase (guards) reads permissions off RbacRole/RbacRoleId instead.
    public Role Role { get; set; } = Role.Employee;
    public string? RbacRoleId { get; set; }
    public RbacRole? RbacRole { get; set; }
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime? LastLoginAt { get; set; }

    // Global-catalog admin (SubscriptionPlan CRUD etc.) - not tenant-facing, no self-service
    // endpoint grants this; it's an internal-ops-only flag set directly in the DB. Distinct
    // from Role.Owner/Admin, which are per-company and irrelevant to global catalog data.
    public bool IsPlatformAdmin { get; set; }

    public string? ManagerId { get; set; }
    public User? Manager { get; set; }
    public string? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? BranchId { get; set; }
    public Branch? Branch { get; set; }

    // Self-reported persona collected at signup (e.g. "Founder", "Sales") - purely for
    // onboarding personalization, unrelated to Role/RbacRole which govern permissions.
    public string? OnboardingRole { get; set; }

    // Doubles as the invite code hash when Status == Invited - registration verification
    // and invite acceptance never overlap for the same user, so one pair of fields covers both.
    public string? EmailVerificationCodeHash { get; set; }
    public DateTime? EmailVerificationCodeExpiresAt { get; set; }

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
    public ICollection<LoginHistory> LoginHistories { get; set; } = new List<LoginHistory>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<User> DirectReports { get; set; } = new List<User>();
}
