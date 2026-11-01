using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppEmbeddedSignupFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "businessId",
                table: "whatsapp_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "connectedByFbUserId",
                table: "whatsapp_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "connectionStatus",
                table: "whatsapp_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "displayPhoneNumber",
                table: "whatsapp_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "encryptedRegistrationPin",
                table: "whatsapp_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "historySyncStatus",
                table: "whatsapp_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "isOnBizApp",
                table: "whatsapp_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "onboardedAt",
                table: "whatsapp_accounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "businessId",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "connectedByFbUserId",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "connectionStatus",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "displayPhoneNumber",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "encryptedRegistrationPin",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "historySyncStatus",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "isOnBizApp",
                table: "whatsapp_accounts");

            migrationBuilder.DropColumn(
                name: "onboardedAt",
                table: "whatsapp_accounts");
        }
    }
}
