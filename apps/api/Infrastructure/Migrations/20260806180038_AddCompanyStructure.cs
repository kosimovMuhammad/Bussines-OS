using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "branchId",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "departmentId",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "managerId",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true),
                    timezone = table.Column<string>(type: "text", nullable: false),
                    isHeadquarters = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                    table.ForeignKey(
                        name: "FK_branches_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    parentDepartmentId = table.Column<string>(type: "text", nullable: true),
                    managerId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.id);
                    table.ForeignKey(
                        name: "FK_departments_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_departments_departments_parentDepartmentId",
                        column: x => x.parentDepartmentId,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_departments_users_managerId",
                        column: x => x.managerId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    departmentId = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    teamLeadId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_teams_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_teams_departments_departmentId",
                        column: x => x.departmentId,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_teams_users_teamLeadId",
                        column: x => x.teamLeadId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "team_members",
                columns: table => new
                {
                    teamId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    joinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_members", x => new { x.teamId, x.userId });
                    table.ForeignKey(
                        name: "FK_team_members_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_members_teams_teamId",
                        column: x => x.teamId,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_members_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_branchId",
                table: "users",
                column: "branchId");

            migrationBuilder.CreateIndex(
                name: "IX_users_departmentId",
                table: "users",
                column: "departmentId");

            migrationBuilder.CreateIndex(
                name: "IX_users_managerId",
                table: "users",
                column: "managerId");

            migrationBuilder.CreateIndex(
                name: "IX_branches_companyId",
                table: "branches",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_branches_companyId_isHeadquarters",
                table: "branches",
                columns: new[] { "companyId", "isHeadquarters" },
                unique: true,
                filter: "\"isHeadquarters\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_departments_companyId",
                table: "departments",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_departments_companyId_name",
                table: "departments",
                columns: new[] { "companyId", "name" },
                unique: true,
                filter: "\"parentDepartmentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_departments_companyId_parentDepartmentId_name",
                table: "departments",
                columns: new[] { "companyId", "parentDepartmentId", "name" },
                unique: true,
                filter: "\"parentDepartmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_departments_managerId",
                table: "departments",
                column: "managerId");

            migrationBuilder.CreateIndex(
                name: "IX_departments_parentDepartmentId",
                table: "departments",
                column: "parentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_team_members_companyId",
                table: "team_members",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_team_members_userId",
                table: "team_members",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_teams_companyId",
                table: "teams",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_teams_departmentId",
                table: "teams",
                column: "departmentId");

            migrationBuilder.CreateIndex(
                name: "IX_teams_teamLeadId",
                table: "teams",
                column: "teamLeadId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_branches_branchId",
                table: "users",
                column: "branchId",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_departments_departmentId",
                table: "users",
                column: "departmentId",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_managerId",
                table: "users",
                column: "managerId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_branches_branchId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_departments_departmentId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_users_managerId",
                table: "users");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "team_members");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropIndex(
                name: "IX_users_branchId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_departmentId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_managerId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "branchId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "departmentId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "managerId",
                table: "users");
        }
    }
}
