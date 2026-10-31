using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstagramModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instagram_accounts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    instagramBusinessAccountId = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    encryptedAccessToken = table.Column<string>(type: "text", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    tokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instagram_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_instagram_accounts_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instagram_conversation_links",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    contactId = table.Column<string>(type: "text", nullable: true),
                    instagramScopedId = table.Column<string>(type: "text", nullable: false),
                    instagramUsername = table.Column<string>(type: "text", nullable: true),
                    linkMethod = table.Column<int>(type: "integer", nullable: false),
                    linkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lastMessagePreview = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instagram_conversation_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_instagram_conversation_links_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_instagram_conversation_links_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_instagram_accounts_companyId",
                table: "instagram_accounts",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_instagram_accounts_instagramBusinessAccountId",
                table: "instagram_accounts",
                column: "instagramBusinessAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_instagram_conversation_links_companyId_instagramScopedId",
                table: "instagram_conversation_links",
                columns: new[] { "companyId", "instagramScopedId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_instagram_conversation_links_contactId",
                table: "instagram_conversation_links",
                column: "contactId");

            // --- Permission catalog expansion for the new Instagram module - same
            // NOT EXISTS-guarded pattern as every prior phase's catalog expansion (see
            // AddTelegramModule).
            migrationBuilder.Sql(@"
                INSERT INTO ""permissions"" (""id"", ""key"", ""module"", ""description"")
                SELECT gen_random_uuid()::text, lower(m.module) || '.' || a.action, m.module, initcap(a.action) || ' ' || m.module
                FROM (VALUES ('Instagram')) AS m(module)
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
                WHERE r.""name"" IN ('Owner', 'Admin') AND p.""module"" = 'Instagram'
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
                  AND p.""module"" = 'Instagram' AND p.""key"" LIKE '%.read'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""role_permissions"" WHERE ""permissionId"" IN (SELECT ""id"" FROM ""permissions"" WHERE ""module"" = 'Instagram');
                DELETE FROM ""permissions"" WHERE ""module"" = 'Instagram';
            ");

            migrationBuilder.DropTable(
                name: "instagram_accounts");

            migrationBuilder.DropTable(
                name: "instagram_conversation_links");
        }
    }
}
