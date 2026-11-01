namespace BusinessOS.Domain.Enums;

// Result of the (multi-step, individually retryable) Embedded Signup onboarding.
public enum WhatsAppConnectionStatus
{
    // Fully onboarded: token stored, app subscribed to the WABA's webhooks.
    Active,

    // Saved, but a post-signup step (register / webhook subscription / sync request) failed and
    // must be retried before the number can send or receive reliably.
    NeedsAttention,
}

// Coexistence only: progress of the one-time contacts + message-history sync that must be
// requested within 24 hours of onboarding.
public enum WhatsAppHistorySyncStatus
{
    NotApplicable,
    Pending,
    Requested,
    Completed,
    Declined,
    Failed,
}
