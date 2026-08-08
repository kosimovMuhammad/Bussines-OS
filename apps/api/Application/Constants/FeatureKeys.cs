namespace BusinessOS.Application.Constants;

// Boolean feature toggles from the seeded plan matrix (Billing Phase 2) - distinct from
// UsageMetricKeys, which are numeric/capped. Every key here should have a
// SubscriptionPlanFeature row (Limit=NULL, Enabled=true/false) on every plan.
public static class FeatureKeys
{
    public const string WhatsAppIntegration = "whatsapp_integration";
    public const string EmailIntegration = "email_integration";
    public const string AiFeatures = "ai_features";
    public const string AdvancedAnalytics = "advanced_analytics";
    public const string CustomBranding = "custom_branding";
    public const string ApiAccess = "api_access";

    public static readonly IReadOnlyList<string> All =
        [WhatsAppIntegration, EmailIntegration, AiFeatures, AdvancedAnalytics, CustomBranding, ApiAccess];
}
