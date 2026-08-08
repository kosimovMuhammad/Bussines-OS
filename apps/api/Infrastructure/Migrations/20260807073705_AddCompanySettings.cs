using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_settings",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    passwordMinLength = table.Column<int>(type: "integer", nullable: false),
                    passwordRequireUppercase = table.Column<bool>(type: "boolean", nullable: false),
                    passwordRequireNumber = table.Column<bool>(type: "boolean", nullable: false),
                    passwordRequireSymbol = table.Column<bool>(type: "boolean", nullable: false),
                    sessionTimeoutMinutes = table.Column<int>(type: "integer", nullable: false),
                    requireTwoFactor = table.Column<bool>(type: "boolean", nullable: false),
                    logoUrl = table.Column<string>(type: "text", nullable: true),
                    primaryColor = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_settings_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_settings_companyId",
                table: "company_settings",
                column: "companyId",
                unique: true);

            // --- Permission catalog expansion for Phase 3's Settings module, same
            // NOT EXISTS-guarded pattern as AddPermissionGuardsPhase/AddSecurityAndVisibilityPhase.
            migrationBuilder.Sql(@"
                INSERT INTO ""permissions"" (""id"", ""key"", ""module"", ""description"")
                SELECT gen_random_uuid()::text, lower(m.module) || '.' || a.action, m.module, initcap(a.action) || ' ' || m.module
                FROM (VALUES ('Settings')) AS m(module)
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
                WHERE r.""name"" IN ('Owner', 'Admin') AND p.""module"" = 'Settings'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");

            // Manager/Member/ReadOnly get read-only on Settings, same default-read-only
            // convention as every other module (Settings isn't sensitive governance data
            // like Audit, so it doesn't get that module's stricter no-grant-at-all default).
            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Manager', 'Member', 'ReadOnly')
                  AND p.""module"" = 'Settings' AND p.""key"" LIKE '%.read'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""role_permissions"" WHERE ""permissionId"" IN (SELECT ""id"" FROM ""permissions"" WHERE ""module"" = 'Settings');
                DELETE FROM ""permissions"" WHERE ""module"" = 'Settings';
            ");

            migrationBuilder.DropTable(
                name: "company_settings");
        }
    }
}
