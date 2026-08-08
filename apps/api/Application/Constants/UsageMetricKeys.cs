namespace BusinessOS.Application.Constants;

public static class UsageMetricKeys
{
    public const string MaxUsers = "max_users";
    public const string MaxContacts = "max_contacts";
    public const string MaxDeals = "max_deals";
    public const string AutomationWorkflows = "automation_workflows";

    // Soft/informational for this pass - tracked on the usage dashboard but not hard
    // blocked (per the requirements' recommended hard-vs-soft split): storage isn't wired
    // to a real quota system yet, and per-message billing isn't implemented either.
    public const string StorageMb = "storage_mb";
    public const string WhatsAppMessagesSentThisMonth = "whatsapp_messages_sent_this_month";

    public static readonly IReadOnlyList<string> HardCapped = [MaxUsers, MaxContacts, MaxDeals, AutomationWorkflows];
}
