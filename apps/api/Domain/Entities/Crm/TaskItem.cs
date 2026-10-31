using BusinessOS.Domain.Interfaces;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class TaskItem : IActivityLoggable, IArchivable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public string? AssignedTo { get; set; }
    public User? Assignee { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? LastDueNotifiedAt { get; set; }
    public DateTime? LastOverdueNotifiedAt { get; set; }
    public string? DealId { get; set; }
    public Deal? Deal { get; set; }
    public string? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}
