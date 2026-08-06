using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class Meeting : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public string? DealId { get; set; }
    public Deal? Deal { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? Location { get; set; }
    public string OrganizerId { get; set; } = string.Empty;
    public User Organizer { get; set; } = null!;
    public List<string> AttendeeUserIds { get; set; } = new();
    public MeetingStatus Status { get; set; } = MeetingStatus.Scheduled;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? GoogleEventId { get; set; }
    public string? GoogleCalendarId { get; set; }
    public MeetingSyncStatus SyncStatus { get; set; } = MeetingSyncStatus.NotSynced;
    public string? SyncError { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}
