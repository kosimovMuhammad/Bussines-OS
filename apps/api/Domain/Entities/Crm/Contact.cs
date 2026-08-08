using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class Contact : IActivityLoggable, IArchivable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Position { get; set; }
    public string? CompanyName { get; set; }
    public string? Notes { get; set; }
    public string? OwnerId { get; set; }
    public User? Owner { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Communication> Communications { get; set; } = new List<Communication>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<FileAsset> Files { get; set; } = new List<FileAsset>();
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
    public ICollection<AiEmailDraft> AiEmailDrafts { get; set; } = new List<AiEmailDraft>();
}
