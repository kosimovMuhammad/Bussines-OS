using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndActivitySystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_companyId",
                table: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "entityId",
                table: "audit_logs",
                newName: "targetId");

            migrationBuilder.RenameColumn(
                name: "entity",
                table: "audit_logs",
                newName: "targetType");

            migrationBuilder.AddColumn<string>(
                name: "ipAddress",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "userAgent",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "activity_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    entityType = table.Column<string>(type: "text", nullable: false),
                    entityId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_logs_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_logs_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "login_histories",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: true),
                    companyId = table.Column<string>(type: "text", nullable: true),
                    loginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ipAddress = table.Column<string>(type: "text", nullable: true),
                    userAgent = table.Column<string>(type: "text", nullable: true),
                    deviceInfo = table.Column<string>(type: "text", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    failureReason = table.Column<string>(type: "text", nullable: true),
                    isNewDevice = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_histories", x => x.id);
                    table.ForeignKey(
                        name: "FK_login_histories_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_login_histories_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_preferences_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_preferences_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    relatedEntityType = table.Column<string>(type: "text", nullable: true),
                    relatedEntityId = table.Column<string>(type: "text", nullable: true),
                    isRead = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    readAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_companyId_createdAt",
                table: "audit_logs",
                columns: new[] { "companyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_companyId_createdAt",
                table: "activity_logs",
                columns: new[] { "companyId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_companyId_entityType_entityId_createdAt",
                table: "activity_logs",
                columns: new[] { "companyId", "entityType", "entityId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_userId",
                table: "activity_logs",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_login_histories_companyId_loginAt",
                table: "login_histories",
                columns: new[] { "companyId", "loginAt" });

            migrationBuilder.CreateIndex(
                name: "IX_login_histories_userId_loginAt",
                table: "login_histories",
                columns: new[] { "userId", "loginAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_companyId",
                table: "notification_preferences",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_userId_type_channel",
                table: "notification_preferences",
                columns: new[] { "userId", "type", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_companyId_userId_isRead_createdAt",
                table: "notifications",
                columns: new[] { "companyId", "userId", "isRead", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_userId",
                table: "notifications",
                column: "userId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_logs");

            migrationBuilder.DropTable(
                name: "login_histories");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_companyId_createdAt",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ipAddress",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "userAgent",
                table: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "targetId",
                table: "audit_logs",
                newName: "entityId");

            migrationBuilder.RenameColumn(
                name: "targetType",
                table: "audit_logs",
                newName: "entity");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_companyId",
                table: "audit_logs",
                column: "companyId");
        }
    }
}
