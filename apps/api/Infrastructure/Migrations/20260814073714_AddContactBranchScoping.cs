using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactBranchScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "branchId",
                table: "contacts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contacts_branchId",
                table: "contacts",
                column: "branchId");

            migrationBuilder.AddForeignKey(
                name: "FK_contacts_branches_branchId",
                table: "contacts",
                column: "branchId",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contacts_branches_branchId",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_branchId",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "branchId",
                table: "contacts");
        }
    }
}
