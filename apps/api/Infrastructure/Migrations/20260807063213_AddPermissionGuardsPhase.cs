using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionGuardsPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    permissionId = table.Column<string>(type: "text", nullable: false),
                    allow = table.Column<bool>(type: "boolean", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_overrides", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_permissions_permissionId",
                        column: x => x.permissionId,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_permission_overrides_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_companyId",
                table: "user_permission_overrides",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_permissionId",
                table: "user_permission_overrides",
                column: "permissionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_userId_permissionId",
                table: "user_permission_overrides",
                columns: new[] { "userId", "permissionId" },
                unique: true);

            // --- Cuts enforcement over to the DB: PermissionAuthorizationFilter now reads
            // RolePermission/UserPermissionOverride instead of the RolePermissions.cs matrix,
            // so the permission catalog needs to actually cover every gated module, not just
            // the original 8. 'Settings' from the AddRbacTables seed was never a real
            // ModuleName value (dead data, never matchable by the filter) - drop it first.
            migrationBuilder.Sql(@"DELETE FROM ""permissions"" WHERE ""module"" = 'Settings';");

            migrationBuilder.Sql(@"
                INSERT INTO ""permissions"" (""id"", ""key"", ""module"", ""description"")
                SELECT gen_random_uuid()::text, lower(m.module) || '.' || a.action, m.module, initcap(a.action) || ' ' || m.module
                FROM (VALUES
                    ('Profile'), ('Users'), ('Company'), ('Contacts'), ('Deals'), ('Tasks'), ('Communications'),
                    ('Invoices'), ('Files'), ('WhatsApp'), ('Email'), ('Calendar'), ('Ai'), ('Dashboard'),
                    ('Analytics'), ('Organization'), ('Automation'), ('Billing'), ('Rbac')
                ) AS m(module)
                CROSS JOIN (VALUES ('read'), ('create'), ('update'), ('delete')) AS a(action)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""permissions"" p WHERE p.""key"" = lower(m.module) || '.' || a.action
                );
            ");

            // Owner/Admin get every permission in the catalog, including the ones just added -
            // NOT EXISTS guards against re-inserting (roleId, permissionId) pairs the original
            // AddRbacTables seed already granted, which would otherwise violate the PK.
            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Owner', 'Admin')
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Manager', 'Member', 'ReadOnly') AND p.""key"" LIKE '%.read'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_permission_overrides");
        }
    }
}
