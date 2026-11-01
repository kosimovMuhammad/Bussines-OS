using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Interfaces;

namespace BusinessOS.Domain.Entities;

// ID cheat-sheet - three different identifiers get confused easily:
//   PhoneNumberId       Meta's id of the number (also the id in POST /{id}/messages and the
//                       webhook routing key `metadata.phone_number_id`). Globally unique.
//   WabaId              the Messaging account (WABA) that owns the number; entry.id of account_update.
//   DisplayPhoneNumber  the human-readable number ("+992 ..."); UI only, NEVER used for routing.
public class WhatsAppAccount : ITenantScoped
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public Company Company { get; set; } = null!;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string WabaId { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string? VerifiedName { get; set; }
    public string? DisplayPhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;

    // Business portfolio id from the Embedded Signup session, and the Facebook user who
    // completed the flow. The latter is what Meta's signed_request (deauthorize / data deletion)
    // carries as user_id, so it is the only way to map such a callback back to this row.
    public string? BusinessId { get; set; }
    public string? ConnectedByFbUserId { get; set; }

    // True when the number is also in use with the WhatsApp Business app (Coexistence) - such
    // numbers are already registered, so the /register step is skipped, throughput is capped at
    // 20 mps and messages sent from the phone arrive as smb_message_echoes.
    public bool IsOnBizApp { get; set; }

    public WhatsAppConnectionStatus ConnectionStatus { get; set; } = WhatsAppConnectionStatus.Active;
    public WhatsAppHistorySyncStatus HistorySyncStatus { get; set; } = WhatsAppHistorySyncStatus.NotApplicable;

    // Two-step-verification PIN set when WE registered the number (non-coexistence only), kept
    // encrypted so a later re-register can supply it. Null for coexistence numbers.
    public string? EncryptedRegistrationPin { get; set; }

    public DateTime? OnboardedAt { get; set; }

    // Long-lived tokens from the fb_exchange_token grant are valid for ~60 days and must be
    // refreshed before they expire - null for tokens connected via the legacy manual-paste
    // path where the expiry isn't known (see InstagramAccount.TokenExpiresAt for the same
    // reasoning, and WhatsAppTokenRefreshService for the refresh job).
    public DateTime? TokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
