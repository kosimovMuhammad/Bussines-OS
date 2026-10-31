using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppAccountTokenLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "isActive",
                table: "whatsapp_accounts",
                type: "boolean",
                nullable: false,
                // Existing rows are already-connected accounts - default them active (matches
                // the entity's C# default) rather than false, which would silently exclude them
                // from the token-refresh job until someone noticed and manually flipped it back.
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "tokenExpiresAt",
                table: "whatsapp_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedAt",
                table: "whatsapp_accounts",
                type: "timestamp with time zone",
                nullable: false,
                // Existing rows have no real "last updated" value - back-fill from createdAt via
                // raw SQL below rather than an arbitrary epoch/year-0001 default.
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("UPDATE whatsapp_accounts SET \"updatedAt\" = \"createdAt\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "isActive",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "tokenExpiresAt",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "updatedAt",
                table: "whatsapp_accounts");
        }
    }
}
