using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    priceMonthly = table.Column<decimal>(type: "numeric", nullable: false),
                    priceYearly = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    sortOrder = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usage_counters",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    metricKey = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    currentValue = table.Column<int>(type: "integer", nullable: false),
                    periodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    periodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_counters", x => x.id);
                    table.ForeignKey(
                        name: "FK_usage_counters_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plan_features",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    planId = table.Column<string>(type: "text", nullable: false),
                    featureKey = table.Column<string>(type: "text", nullable: false),
                    limit = table.Column<int>(type: "integer", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plan_features", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscription_plan_features_subscription_plans_planId",
                        column: x => x.planId,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    planId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    billingCycle = table.Column<int>(type: "integer", nullable: false),
                    currentPeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    currentPeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    trialEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    canceledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscriptions_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscriptions_subscription_plans_planId",
                        column: x => x.planId,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_invoices",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    subscriptionId = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    periodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    periodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    issuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paymentProviderRef = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscription_invoices_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscription_invoices_subscriptions_subscriptionId",
                        column: x => x.subscriptionId,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    subscriptionInvoiceId = table.Column<string>(type: "text", nullable: true),
                    provider = table.Column<string>(type: "text", nullable: false),
                    providerTransactionId = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_transactions_subscription_invoices_subscriptionInvo~",
                        column: x => x.subscriptionInvoiceId,
                        principalTable: "subscription_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_companyId_createdAt",
                table: "payment_transactions",
                columns: new[] { "companyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_providerTransactionId",
                table: "payment_transactions",
                column: "providerTransactionId",
                unique: true,
                filter: "\"providerTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_subscriptionInvoiceId",
                table: "payment_transactions",
                column: "subscriptionInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_invoices_companyId_issuedAt",
                table: "subscription_invoices",
                columns: new[] { "companyId", "issuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_invoices_subscriptionId",
                table: "subscription_invoices",
                column: "subscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plan_features_planId_featureKey",
                table: "subscription_plan_features",
                columns: new[] { "planId", "featureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_isActive_sortOrder",
                table: "subscription_plans",
                columns: new[] { "isActive", "sortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_companyId",
                table: "subscriptions",
                column: "companyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_planId",
                table: "subscriptions",
                column: "planId");

            migrationBuilder.CreateIndex(
                name: "IX_usage_counters_companyId_metricKey",
                table: "usage_counters",
                columns: new[] { "companyId", "metricKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions");

            migrationBuilder.DropTable(
                name: "subscription_plan_features");

            migrationBuilder.DropTable(
                name: "usage_counters");

            migrationBuilder.DropTable(
                name: "subscription_invoices");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "subscription_plans");
        }
    }
}
