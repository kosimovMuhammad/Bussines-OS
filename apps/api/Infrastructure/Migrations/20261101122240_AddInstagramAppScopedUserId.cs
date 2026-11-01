using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstagramAppScopedUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "appScopedUserId",
                table: "instagram_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_instagram_accounts_appScopedUserId",
                table: "instagram_accounts",
                column: "appScopedUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_instagram_accounts_appScopedUserId",
                table: "instagram_accounts");

            migrationBuilder.DropColumn(
                name: "appScopedUserId",
                table: "instagram_accounts");
        }
    }
}
