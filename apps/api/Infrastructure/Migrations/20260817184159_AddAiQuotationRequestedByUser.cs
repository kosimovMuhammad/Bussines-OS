using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiQuotationRequestedByUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "requestedByUserId",
                table: "ai_quotations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_quotations_requestedByUserId",
                table: "ai_quotations",
                column: "requestedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ai_quotations_users_requestedByUserId",
                table: "ai_quotations",
                column: "requestedByUserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_quotations_users_requestedByUserId",
                table: "ai_quotations");

            migrationBuilder.DropIndex(
                name: "IX_ai_quotations_requestedByUserId",
                table: "ai_quotations");

            migrationBuilder.DropColumn(
                name: "requestedByUserId",
                table: "ai_quotations");
        }
    }
}
