using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceDocsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paidById = table.Column<string>(type: "text", nullable: false),
                    branchId = table.Column<string>(type: "text", nullable: true),
                    receiptFileId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.id);
                    table.ForeignKey(
                        name: "FK_expenses_branches_branchId",
                        column: x => x.branchId,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_expenses_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expenses_file_assets_receiptFileId",
                        column: x => x.receiptFileId,
                        principalTable: "file_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_expenses_users_paidById",
                        column: x => x.paidById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "templates",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_templates_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    counterpartyName = table.Column<string>(type: "text", nullable: false),
                    contactId = table.Column<string>(type: "text", nullable: true),
                    dealId = table.Column<string>(type: "text", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    effectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    templateId = table.Column<string>(type: "text", nullable: true),
                    signedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    signedByUserId = table.Column<string>(type: "text", nullable: true),
                    signedIpAddress = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.id);
                    table.ForeignKey(
                        name: "FK_contracts_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contracts_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contracts_deals_dealId",
                        column: x => x.dealId,
                        principalTable: "deals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contracts_templates_templateId",
                        column: x => x.templateId,
                        principalTable: "templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contracts_users_signedByUserId",
                        column: x => x.signedByUserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contracts_companyId",
                table: "contracts",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_contactId",
                table: "contracts",
                column: "contactId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_dealId",
                table: "contracts",
                column: "dealId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_signedByUserId",
                table: "contracts",
                column: "signedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_status",
                table: "contracts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_templateId",
                table: "contracts",
                column: "templateId");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_branchId",
                table: "expenses",
                column: "branchId");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_companyId",
                table: "expenses",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_date",
                table: "expenses",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_paidById",
                table: "expenses",
                column: "paidById");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_receiptFileId",
                table: "expenses",
                column: "receiptFileId");

            migrationBuilder.CreateIndex(
                name: "IX_templates_companyId",
                table: "templates",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_templates_type",
                table: "templates",
                column: "type");

            // --- Permission catalog expansion for Phase 5's 3 new modules (Expenses,
            // Contracts, Templates - Reports deliberately reuses Analytics, see
            // ReportsController's note), same NOT EXISTS-guarded pattern as every prior
            // phase's catalog expansion.
            migrationBuilder.Sql(@"
                INSERT INTO ""permissions"" (""id"", ""key"", ""module"", ""description"")
                SELECT gen_random_uuid()::text, lower(m.module) || '.' || a.action, m.module, initcap(a.action) || ' ' || m.module
                FROM (VALUES ('Expenses'), ('Contracts'), ('Templates')) AS m(module)
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
                WHERE r.""name"" IN ('Owner', 'Admin') AND p.""module"" IN ('Expenses', 'Contracts', 'Templates')
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
                  AND p.""module"" IN ('Expenses', 'Contracts', 'Templates') AND p.""key"" LIKE '%.read'
                  AND NOT EXISTS (
                    SELECT 1 FROM ""role_permissions"" rp WHERE rp.""roleId"" = r.""id"" AND rp.""permissionId"" = p.""id""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""role_permissions"" WHERE ""permissionId"" IN (
                    SELECT ""id"" FROM ""permissions"" WHERE ""module"" IN ('Expenses', 'Contracts', 'Templates')
                );
                DELETE FROM ""permissions"" WHERE ""module"" IN ('Expenses', 'Contracts', 'Templates');
            ");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "templates");
        }
    }
}
