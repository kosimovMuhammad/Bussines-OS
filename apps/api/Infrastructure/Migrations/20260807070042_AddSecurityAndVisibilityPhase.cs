using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAndVisibilityPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deviceInfo",
                table: "refresh_tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ipAddress",
                table: "refresh_tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "userAgent",
                table: "refresh_tokens",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    keyPrefix = table.Column<string>(type: "text", nullable: false),
                    keyHash = table.Column<string>(type: "text", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdByUserId = table.Column<string>(type: "text", nullable: true),
                    lastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_api_keys_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_api_keys_users_createdByUserId",
                        column: x => x.createdByUserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_companyId",
                table: "api_keys",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_createdByUserId",
                table: "api_keys",
                column: "createdByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_keyHash",
                table: "api_keys",
                column: "keyHash",
                unique: true);

            // --- Permission catalog expansion for Phase 2's 5 new modules (Notifications,
            // Activity, Audit, Sessions, ApiKeys), same NOT EXISTS-guarded pattern as the
            // Phase 1 (AddPermissionGuardsPhase) catalog expansion.
            migrationBuilder.Sql(@"
                INSERT INTO ""permissions"" (""id"", ""key"", ""module"", ""description"")
                SELECT gen_random_uuid()::text, lower(m.module) || '.' || a.action, m.module, initcap(a.action) || ' ' || m.module
                FROM (VALUES ('Notifications'), ('Activity'), ('Audit'), ('Sessions'), ('ApiKeys')) AS m(module)
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
                WHERE r.""name"" IN ('Owner', 'Admin') AND p.""module"" IN ('Notifications', 'Activity', 'Audit', 'Sessions', 'ApiKeys')
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");

            // Manager/Member/ReadOnly get read-only on Notifications/Activity/Sessions/ApiKeys,
            // same default-read-only convention as every other module - but NOT Audit. Audit is
            // sensitive governance data (login history, permission changes): non-Owner/Admin
            // roles get no catalog grant at all by default, on top of the hardcoded
            // Owner/Admin-only check in AuditLogService (defense in depth).
            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Manager', 'Member', 'ReadOnly')
                  AND p.""module"" IN ('Notifications', 'Activity', 'Sessions', 'ApiKeys') AND p.""key"" LIKE '%.read'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropColumn(
                name: "deviceInfo",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "ipAddress",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "userAgent",
                table: "refresh_tokens");
        }
    }
}
