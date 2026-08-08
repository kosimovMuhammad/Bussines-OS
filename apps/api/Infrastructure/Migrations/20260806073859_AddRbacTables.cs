using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRbacTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rbacRoleId",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    isSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    roleId = table.Column<string>(type: "text", nullable: false),
                    permissionId = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.roleId, x.permissionId });
                    table.ForeignKey(
                        name: "FK_role_permissions_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permissionId",
                        column: x => x.permissionId,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_roleId",
                        column: x => x.roleId,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_rbacRoleId",
                table: "users",
                column: "rbacRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_key",
                table: "permissions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_module",
                table: "permissions",
                column: "module");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_companyId",
                table: "role_permissions",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_permissionId",
                table: "role_permissions",
                column: "permissionId");

            migrationBuilder.CreateIndex(
                name: "IX_roles_companyId_name",
                table: "roles",
                columns: new[] { "companyId", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_rbacRoleId",
                table: "users",
                column: "rbacRoleId",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // --- Data seed: permission catalog, per-company system roles, and a
            // behavior-preserving backfill of users.rbacRoleId from the legacy
            // integer Role enum. Nothing here changes runtime behavior yet - the
            // authorization filter still reads users.role until the guards phase
            // cuts enforcement over to these tables.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.Sql(@"
                INSERT INTO ""permissions"" (""id"", ""key"", ""module"", ""description"")
                SELECT gen_random_uuid()::text, lower(m.module) || '.' || a.action, m.module, initcap(a.action) || ' ' || m.module
                FROM (VALUES ('Contacts'), ('Deals'), ('Tasks'), ('Communications'), ('Analytics'), ('Settings'), ('Billing'), ('Users')) AS m(module)
                CROSS JOIN (VALUES ('read'), ('create'), ('update'), ('delete')) AS a(action);
            ");

            // Old matrix (RolePermissions.cs) gave Owner/Admin full CRUD and every
            // other role (Manager/Sales/Support/Employee/ReadOnly) read-only on every
            // module - so collapsing Sales/Support/Employee into "Member" here is
            // behavior-preserving, not a permission change.
            migrationBuilder.Sql(@"
                INSERT INTO ""roles"" (""id"", ""companyId"", ""name"", ""description"", ""isSystemRole"", ""createdAt"", ""updatedAt"")
                SELECT gen_random_uuid()::text, c.""id"", r.name, r.description, true, now(), now()
                FROM ""companies"" c
                CROSS JOIN (VALUES
                    ('Owner', 'Full access to everything, including billing and ownership transfer'),
                    ('Admin', 'Full access to everything except ownership transfer'),
                    ('Manager', 'Read-only access across all modules'),
                    ('Member', 'Read-only access across all modules'),
                    ('ReadOnly', 'Read-only access across all modules')
                ) AS r(name, description);
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Owner', 'Admin');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""role_permissions"" (""roleId"", ""permissionId"", ""companyId"", ""createdAt"")
                SELECT r.""id"", p.""id"", r.""companyId"", now()
                FROM ""roles"" r
                CROSS JOIN ""permissions"" p
                WHERE r.""name"" IN ('Manager', 'Member', 'ReadOnly') AND p.""key"" LIKE '%.read';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""users"" u
                SET ""rbacRoleId"" = r.""id""
                FROM ""roles"" r
                WHERE r.""companyId"" = u.""companyId""
                  AND r.""name"" = CASE u.""role""
                    WHEN 0 THEN 'Owner'
                    WHEN 1 THEN 'Admin'
                    WHEN 2 THEN 'Manager'
                    WHEN 3 THEN 'Member'
                    WHEN 4 THEN 'Member'
                    WHEN 5 THEN 'Member'
                    WHEN 6 THEN 'ReadOnly'
                  END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_rbacRoleId",
                table: "users");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropIndex(
                name: "IX_users_rbacRoleId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "rbacRoleId",
                table: "users");
        }
    }
}
