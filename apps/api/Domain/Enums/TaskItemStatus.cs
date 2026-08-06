namespace BusinessOS.Domain.Enums;

// Named TaskItemStatus, not TaskStatus, to avoid colliding with System.Threading.Tasks.TaskStatus.
public enum TaskItemStatus
{
    Todo,
    InProgress,
    Completed,
    Archived,
}
