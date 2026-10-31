using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "telegram_accounts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    encryptedBotToken = table.Column<string>(type: "text", nullable: false),
                    botUsername = table.Column<string>(type: "text", nullable: false),
                    webhookSecret = table.Column<string>(type: "text", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_telegram_accounts_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telegram_chat_links",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    contactId = table.Column<string>(type: "text", nullable: true),
                    chatId = table.Column<long>(type: "bigint", nullable: false),
                    telegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    telegramUsername = table.Column<string>(type: "text", nullable: true),
                    telegramFirstName = table.Column<string>(type: "text", nullable: true),
                    telegramLastName = table.Column<string>(type: "text", nullable: true),
                    phoneNumber = table.Column<string>(type: "text", nullable: true),
                    linkMethod = table.Column<int>(type: "integer", nullable: false),
                    linkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lastMessagePreview = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_chat_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_telegram_chat_links_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_telegram_chat_links_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_accounts_botUsername",
                table: "telegram_accounts",
                column: "botUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_accounts_companyId",
                table: "telegram_accounts",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_accounts_webhookSecret",
                table: "telegram_accounts",
                column: "webhookSecret",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_chat_links_companyId_chatId",
                table: "telegram_chat_links",
                columns: new[] { "companyId", "chatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_chat_links_contactId",
                table: "telegram_chat_links",
                column: "contactId");

            // --- Permission catalog expansion for the new Telegram module - same
            // NOT EXISTS-guarded pattern as every prior phase's catalog expansion (see
            // AddCrossCuttingOpsPhase6's Webhooks block).
            migrationBuilder.Sql(@"
                INSERT INTO ""permissions"" (""id"", ""key"", ""module"", ""description"")
                SELECT gen_random_uuid()::text, lower(m.module) || '.' || a.action, m.module, initcap(a.action) || ' ' || m.module
                FROM (VALUES ('Telegram')) AS m(module)
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
                WHERE r.""name"" IN ('Owner', 'Admin') AND p.""module"" = 'Telegram'
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
                  AND p.""module"" = 'Telegram' AND p.""key"" LIKE '%.read'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""role_permissions"" WHERE ""permissionId"" IN (SELECT ""id"" FROM ""permissions"" WHERE ""module"" = 'Telegram');
                DELETE FROM ""permissions"" WHERE ""module"" = 'Telegram';
            ");

            migrationBuilder.DropTable(
                name: "telegram_accounts");

            migrationBuilder.DropTable(
                name: "telegram_chat_links");
        }
    }
}
