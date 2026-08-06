using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

public class WhatsAppAccount : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string WabaId { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string? VerifiedName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
