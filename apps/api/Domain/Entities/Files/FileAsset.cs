using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class FileAsset : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string? ContactId { get; set; }
    public Contact? Contact { get; set; }
    public string? DealId { get; set; }
    public Deal? Deal { get; set; }
    public string? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public string UploadedById { get; set; } = string.Empty;
    public User UploadedBy { get; set; } = null!;
    public string S3Key { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AiQuotation? AiQuotation { get; set; }
}
