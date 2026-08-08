using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Tier/pricing matrix approved by the user before this migration was written (see the
    // Subscription & Billing Phase 2 plan) - a one-time data seed, no schema changes.
    // Prices are in TJS (the Company entity's default currency, targeting the Tajikistan/
    // CIS SMB market this product is built for).
    public partial class SeedSubscriptionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""subscription_plans"" (""id"", ""name"", ""description"", ""priceMonthly"", ""priceYearly"", ""currency"", ""isActive"", ""sortOrder"", ""createdAt"", ""updatedAt"")
                VALUES
                    ('plan-free', 'Free', 'For trying things out with a very small team', 0, 0, 'TJS', true, 0, now(), now()),
                    ('plan-starter', 'Starter', 'For small teams getting organized', 250, 2500, 'TJS', true, 1, now(), now()),
                    ('plan-pro', 'Pro', 'For growing teams that need automation and insight', 650, 6500, 'TJS', true, 2, now(), now()),
                    ('plan-enterprise', 'Enterprise', 'For larger organizations needing full control', 1500, 15000, 'TJS', true, 3, now(), now());

                INSERT INTO ""subscription_plan_features"" (""id"", ""planId"", ""featureKey"", ""limit"", ""enabled"")
                VALUES
                    -- Free
                    ('plan-free-max_users', 'plan-free', 'max_users', 2, true),
                    ('plan-free-max_contacts', 'plan-free', 'max_contacts', 100, true),
                    ('plan-free-max_deals', 'plan-free', 'max_deals', 50, true),
                    ('plan-free-storage_mb', 'plan-free', 'storage_mb', 100, true),
                    ('plan-free-automation_workflows', 'plan-free', 'automation_workflows', 1, true),
                    ('plan-free-whatsapp_integration', 'plan-free', 'whatsapp_integration', NULL, false),
                    ('plan-free-email_integration', 'plan-free', 'email_integration', NULL, true),
                    ('plan-free-ai_features', 'plan-free', 'ai_features', NULL, false),
                    ('plan-free-advanced_analytics', 'plan-free', 'advanced_analytics', NULL, false),
                    ('plan-free-custom_branding', 'plan-free', 'custom_branding', NULL, false),
                    ('plan-free-api_access', 'plan-free', 'api_access', NULL, false),

                    -- Starter
                    ('plan-starter-max_users', 'plan-starter', 'max_users', 5, true),
                    ('plan-starter-max_contacts', 'plan-starter', 'max_contacts', 1000, true),
                    ('plan-starter-max_deals', 'plan-starter', 'max_deals', NULL, true),
                    ('plan-starter-storage_mb', 'plan-starter', 'storage_mb', 1000, true),
                    ('plan-starter-automation_workflows', 'plan-starter', 'automation_workflows', 3, true),
                    ('plan-starter-whatsapp_integration', 'plan-starter', 'whatsapp_integration', NULL, true),
                    ('plan-starter-email_integration', 'plan-starter', 'email_integration', NULL, true),
                    ('plan-starter-ai_features', 'plan-starter', 'ai_features', NULL, false),
                    ('plan-starter-advanced_analytics', 'plan-starter', 'advanced_analytics', NULL, false),
                    ('plan-starter-custom_branding', 'plan-starter', 'custom_branding', NULL, false),
                    ('plan-starter-api_access', 'plan-starter', 'api_access', NULL, false),

                    -- Pro
                    ('plan-pro-max_users', 'plan-pro', 'max_users', 20, true),
                    ('plan-pro-max_contacts', 'plan-pro', 'max_contacts', NULL, true),
                    ('plan-pro-max_deals', 'plan-pro', 'max_deals', NULL, true),
                    ('plan-pro-storage_mb', 'plan-pro', 'storage_mb', 10000, true),
                    ('plan-pro-automation_workflows', 'plan-pro', 'automation_workflows', 15, true),
                    ('plan-pro-whatsapp_integration', 'plan-pro', 'whatsapp_integration', NULL, true),
                    ('plan-pro-email_integration', 'plan-pro', 'email_integration', NULL, true),
                    ('plan-pro-ai_features', 'plan-pro', 'ai_features', NULL, true),
                    ('plan-pro-advanced_analytics', 'plan-pro', 'advanced_analytics', NULL, true),
                    ('plan-pro-custom_branding', 'plan-pro', 'custom_branding', NULL, false),
                    ('plan-pro-api_access', 'plan-pro', 'api_access', NULL, false),

                    -- Enterprise
                    ('plan-enterprise-max_users', 'plan-enterprise', 'max_users', NULL, true),
                    ('plan-enterprise-max_contacts', 'plan-enterprise', 'max_contacts', NULL, true),
                    ('plan-enterprise-max_deals', 'plan-enterprise', 'max_deals', NULL, true),
                    ('plan-enterprise-storage_mb', 'plan-enterprise', 'storage_mb', NULL, true),
                    ('plan-enterprise-automation_workflows', 'plan-enterprise', 'automation_workflows', NULL, true),
                    ('plan-enterprise-whatsapp_integration', 'plan-enterprise', 'whatsapp_integration', NULL, true),
                    ('plan-enterprise-email_integration', 'plan-enterprise', 'email_integration', NULL, true),
                    ('plan-enterprise-ai_features', 'plan-enterprise', 'ai_features', NULL, true),
                    ('plan-enterprise-advanced_analytics', 'plan-enterprise', 'advanced_analytics', NULL, true),
                    ('plan-enterprise-custom_branding', 'plan-enterprise', 'custom_branding', NULL, true),
                    ('plan-enterprise-api_access', 'plan-enterprise', 'api_access', NULL, true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""subscription_plan_features"" WHERE ""planId"" IN ('plan-free', 'plan-starter', 'plan-pro', 'plan-enterprise');
                DELETE FROM ""subscription_plans"" WHERE ""id"" IN ('plan-free', 'plan-starter', 'plan-pro', 'plan-enterprise');
            ");
        }
    }
}
