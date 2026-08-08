using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossCuttingOpsPhase6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "purchase_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "expenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "deals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isArchived",
                table: "categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "file_versions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    fileAssetId = table.Column<string>(type: "text", nullable: false),
                    versionNumber = table.Column<int>(type: "integer", nullable: false),
                    s3Key = table.Column<string>(type: "text", nullable: false),
                    filename = table.Column<string>(type: "text", nullable: false),
                    mimeType = table.Column<string>(type: "text", nullable: false),
                    sizeBytes = table.Column<int>(type: "integer", nullable: false),
                    uploadedById = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_file_versions_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_file_versions_file_assets_fileAssetId",
                        column: x => x.fileAssetId,
                        principalTable: "file_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_file_versions_users_uploadedById",
                        column: x => x.uploadedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "webhooks",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    secretEncrypted = table.Column<string>(type: "text", nullable: false),
                    subscribedEvents = table.Column<List<string>>(type: "text[]", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhooks", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhooks_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_delivery_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    webhookId = table.Column<string>(type: "text", nullable: true),
                    @event = table.Column<string>(name: "event", type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    statusCode = table.Column<int>(type: "integer", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    errorMessage = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_delivery_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_logs_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_webhook_delivery_logs_webhooks_webhookId",
                        column: x => x.webhookId,
                        principalTable: "webhooks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_file_versions_companyId",
                table: "file_versions",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_file_versions_fileAssetId_versionNumber",
                table: "file_versions",
                columns: new[] { "fileAssetId", "versionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_versions_uploadedById",
                table: "file_versions",
                column: "uploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_logs_companyId_createdAt",
                table: "webhook_delivery_logs",
                columns: new[] { "companyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_delivery_logs_webhookId",
                table: "webhook_delivery_logs",
                column: "webhookId");

            migrationBuilder.CreateIndex(
                name: "IX_webhooks_companyId",
                table: "webhooks",
                column: "companyId");

            // --- Permission catalog expansion for Phase 6's one new module (Webhooks) -
            // Archive/Restore/Bulk/Import/Export/Timeline all reuse each entity's EXISTING
            // module permissions (e.g. archiving a Contact needs contacts.update, same as
            // any other Contact mutation) rather than introducing new permission keys, same
            // NOT EXISTS-guarded pattern as every prior phase's catalog expansion.
            migrationBuilder.Sql(@"
                INSERT INTO ""permissions"" (""id"", ""key"", ""module"", ""description"")
                SELECT gen_random_uuid()::text, lower(m.module) || '.' || a.action, m.module, initcap(a.action) || ' ' || m.module
                FROM (VALUES ('Webhooks')) AS m(module)
                CROSS JOIN (VALUES ('read'), ('create'), ('update'), ('delete')) AS a(action)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""permissions"" p WHERE p.""key"" = lower(m.module) || '.' || a.action
                );
            ");

            // Owner/Admin get every new permission, same as every other module.
            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Owner', 'Admin') AND p.""module"" = 'Webhooks'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");

            // Manager/Member/ReadOnly get read-only, same default-read-only convention as
            // every other non-Audit module.
            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Manager', 'Member', 'ReadOnly')
                  AND p.""module"" = 'Webhooks' AND p.""key"" LIKE '%.read'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""role_permissions"" WHERE ""permissionId"" IN (SELECT ""id"" FROM ""permissions"" WHERE ""module"" = 'Webhooks');
                DELETE FROM ""permissions"" WHERE ""module"" = 'Webhooks';
            ");

            migrationBuilder.DropTable(
                name: "file_versions");

            migrationBuilder.DropTable(
                name: "webhook_delivery_logs");

            migrationBuilder.DropTable(
                name: "webhooks");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "products");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "contracts");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "isArchived",
                table: "categories");
        }
    }
}
