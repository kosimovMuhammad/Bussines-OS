using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    industry = table.Column<string>(type: "text", nullable: true),
                    plan = table.Column<int>(type: "integer", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_query_results",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    intent = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resultRows = table.Column<string>(type: "jsonb", nullable: true),
                    answer = table.Column<string>(type: "text", nullable: true),
                    errorMessage = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_query_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_query_results_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    passwordHash = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<int>(type: "integer", nullable: false),
                    avatarUrl = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    lastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    inviteTokenHash = table.Column<string>(type: "text", nullable: true),
                    inviteTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_accounts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    phoneNumberId = table.Column<string>(type: "text", nullable: false),
                    wabaId = table.Column<string>(type: "text", nullable: false),
                    encryptedAccessToken = table.Column<string>(type: "text", nullable: false),
                    verifiedName = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_whatsapp_accounts_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity = table.Column<string>(type: "text", nullable: false),
                    entityId = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "calendar_sync_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    resourceId = table.Column<string>(type: "text", nullable: false),
                    syncToken = table.Column<string>(type: "text", nullable: true),
                    channelId = table.Column<string>(type: "text", nullable: false),
                    channelToken = table.Column<string>(type: "text", nullable: false),
                    channelExpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_sync_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_calendar_sync_tokens_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calendar_sync_tokens_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    firstName = table.Column<string>(type: "text", nullable: false),
                    lastName = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    position = table.Column<string>(type: "text", nullable: true),
                    companyName = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    ownerId = table.Column<string>(type: "text", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_contacts_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contacts_users_ownerId",
                        column: x => x.ownerId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "oauth_connections",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<int>(type: "integer", nullable: false),
                    encryptedAccessToken = table.Column<string>(type: "text", nullable: false),
                    encryptedRefreshToken = table.Column<string>(type: "text", nullable: true),
                    scope = table.Column<string>(type: "text", nullable: true),
                    expiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_connections", x => x.id);
                    table.ForeignKey(
                        name: "FK_oauth_connections_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_oauth_connections_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    tokenHash = table.Column<string>(type: "text", nullable: false),
                    expiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked = table.Column<bool>(type: "boolean", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_email_drafts",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    contactId = table.Column<string>(type: "text", nullable: false),
                    context = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: true),
                    body = table.Column<string>(type: "text", nullable: true),
                    errorMessage = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_email_drafts", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_email_drafts_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_email_drafts_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "communications",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    contactId = table.Column<string>(type: "text", nullable: true),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    attachments = table.Column<string>(type: "jsonb", nullable: true),
                    providerMessageId = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communications", x => x.id);
                    table.ForeignKey(
                        name: "FK_communications_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_communications_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "deals",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    contactId = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    stage = table.Column<int>(type: "integer", nullable: false),
                    probability = table.Column<int>(type: "integer", nullable: false),
                    expectedCloseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ownerId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deals", x => x.id);
                    table.ForeignKey(
                        name: "FK_deals_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_deals_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deals_users_ownerId",
                        column: x => x.ownerId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_summaries",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    communicationId = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    sentiment = table.Column<int>(type: "integer", nullable: true),
                    actionItems = table.Column<string>(type: "jsonb", nullable: true),
                    nextFollowUp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    generatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_summaries_communications_communicationId",
                        column: x => x.communicationId,
                        principalTable: "communications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    dealId = table.Column<string>(type: "text", nullable: true),
                    contactId = table.Column<string>(type: "text", nullable: false),
                    invoiceNumber = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    issueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoices_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_deals_dealId",
                        column: x => x.dealId,
                        principalTable: "deals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "meetings",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    contactId = table.Column<string>(type: "text", nullable: true),
                    dealId = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    startAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    endAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "text", nullable: true),
                    organizerId = table.Column<string>(type: "text", nullable: false),
                    attendeeUserIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    googleEventId = table.Column<string>(type: "text", nullable: true),
                    googleCalendarId = table.Column<string>(type: "text", nullable: true),
                    syncStatus = table.Column<int>(type: "integer", nullable: false),
                    syncError = table.Column<string>(type: "text", nullable: true),
                    lastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meetings", x => x.id);
                    table.ForeignKey(
                        name: "FK_meetings_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meetings_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_meetings_deals_dealId",
                        column: x => x.dealId,
                        principalTable: "deals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_meetings_users_organizerId",
                        column: x => x.organizerId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    assignedTo = table.Column<string>(type: "text", nullable: true),
                    dueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dealId = table.Column<string>(type: "text", nullable: true),
                    contactId = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_tasks_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tasks_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tasks_deals_dealId",
                        column: x => x.dealId,
                        principalTable: "deals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tasks_users_assignedTo",
                        column: x => x.assignedTo,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "file_assets",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    contactId = table.Column<string>(type: "text", nullable: true),
                    dealId = table.Column<string>(type: "text", nullable: true),
                    invoiceId = table.Column<string>(type: "text", nullable: true),
                    uploadedById = table.Column<string>(type: "text", nullable: false),
                    s3Key = table.Column<string>(type: "text", nullable: false),
                    filename = table.Column<string>(type: "text", nullable: false),
                    mimeType = table.Column<string>(type: "text", nullable: false),
                    sizeBytes = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_assets", x => x.id);
                    table.ForeignKey(
                        name: "FK_file_assets_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_file_assets_contacts_contactId",
                        column: x => x.contactId,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_file_assets_deals_dealId",
                        column: x => x.dealId,
                        principalTable: "deals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_file_assets_invoices_invoiceId",
                        column: x => x.invoiceId,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_file_assets_users_uploadedById",
                        column: x => x.uploadedById,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    invoiceId = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    paidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payments_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payments_invoices_invoiceId",
                        column: x => x.invoiceId,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "time_entries",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    taskId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: true),
                    startedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    stoppedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    durationSec = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_time_entries_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_time_entries_tasks_taskId",
                        column: x => x.taskId,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_time_entries_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_quotations",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    dealId = table.Column<string>(type: "text", nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: false),
                    totalAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    fileAssetId = table.Column<string>(type: "text", nullable: true),
                    errorMessage = table.Column<string>(type: "text", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_quotations", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_quotations_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_quotations_deals_dealId",
                        column: x => x.dealId,
                        principalTable: "deals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_quotations_file_assets_fileAssetId",
                        column: x => x.fileAssetId,
                        principalTable: "file_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_email_drafts_companyId",
                table: "ai_email_drafts",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_email_drafts_contactId",
                table: "ai_email_drafts",
                column: "contactId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_query_results_companyId",
                table: "ai_query_results",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_quotations_companyId",
                table: "ai_quotations",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_quotations_dealId",
                table: "ai_quotations",
                column: "dealId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_quotations_fileAssetId",
                table: "ai_quotations",
                column: "fileAssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_summaries_communicationId",
                table: "ai_summaries",
                column: "communicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_companyId",
                table: "audit_logs",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_userId",
                table: "audit_logs",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_sync_tokens_channelId",
                table: "calendar_sync_tokens",
                column: "channelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_sync_tokens_companyId",
                table: "calendar_sync_tokens",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_calendar_sync_tokens_userId",
                table: "calendar_sync_tokens",
                column: "userId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_communications_channel",
                table: "communications",
                column: "channel");

            migrationBuilder.CreateIndex(
                name: "IX_communications_companyId_contactId",
                table: "communications",
                columns: new[] { "companyId", "contactId" });

            migrationBuilder.CreateIndex(
                name: "IX_communications_contactId",
                table: "communications",
                column: "contactId");

            migrationBuilder.CreateIndex(
                name: "IX_communications_providerMessageId",
                table: "communications",
                column: "providerMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contacts_companyId",
                table: "contacts",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_email",
                table: "contacts",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_ownerId",
                table: "contacts",
                column: "ownerId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_phone",
                table: "contacts",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "IX_deals_companyId",
                table: "deals",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_deals_contactId",
                table: "deals",
                column: "contactId");

            migrationBuilder.CreateIndex(
                name: "IX_deals_ownerId",
                table: "deals",
                column: "ownerId");

            migrationBuilder.CreateIndex(
                name: "IX_deals_stage",
                table: "deals",
                column: "stage");

            migrationBuilder.CreateIndex(
                name: "IX_file_assets_companyId",
                table: "file_assets",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_file_assets_contactId",
                table: "file_assets",
                column: "contactId");

            migrationBuilder.CreateIndex(
                name: "IX_file_assets_dealId",
                table: "file_assets",
                column: "dealId");

            migrationBuilder.CreateIndex(
                name: "IX_file_assets_invoiceId",
                table: "file_assets",
                column: "invoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_file_assets_uploadedById",
                table: "file_assets",
                column: "uploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_companyId",
                table: "invoices",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_companyId_invoiceNumber",
                table: "invoices",
                columns: new[] { "companyId", "invoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_contactId",
                table: "invoices",
                column: "contactId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_dealId",
                table: "invoices",
                column: "dealId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_status",
                table: "invoices",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_meetings_companyId",
                table: "meetings",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_meetings_contactId",
                table: "meetings",
                column: "contactId");

            migrationBuilder.CreateIndex(
                name: "IX_meetings_dealId",
                table: "meetings",
                column: "dealId");

            migrationBuilder.CreateIndex(
                name: "IX_meetings_googleEventId",
                table: "meetings",
                column: "googleEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meetings_organizerId",
                table: "meetings",
                column: "organizerId");

            migrationBuilder.CreateIndex(
                name: "IX_meetings_startAt",
                table: "meetings",
                column: "startAt");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_connections_companyId",
                table: "oauth_connections",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_connections_userId_provider",
                table: "oauth_connections",
                columns: new[] { "userId", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_companyId",
                table: "payments",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoiceId",
                table: "payments",
                column: "invoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_userId",
                table: "refresh_tokens",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_assignedTo",
                table: "tasks",
                column: "assignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_companyId",
                table: "tasks",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_contactId",
                table: "tasks",
                column: "contactId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_dealId",
                table: "tasks",
                column: "dealId");

            migrationBuilder.CreateIndex(
                name: "IX_time_entries_companyId",
                table: "time_entries",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_time_entries_taskId",
                table: "time_entries",
                column: "taskId");

            migrationBuilder.CreateIndex(
                name: "IX_time_entries_userId",
                table: "time_entries",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_users_companyId",
                table: "users",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_accounts_companyId",
                table: "whatsapp_accounts",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_accounts_phoneNumberId",
                table: "whatsapp_accounts",
                column: "phoneNumberId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_email_drafts");

            migrationBuilder.DropTable(
                name: "ai_query_results");

            migrationBuilder.DropTable(
                name: "ai_quotations");

            migrationBuilder.DropTable(
                name: "ai_summaries");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "calendar_sync_tokens");

            migrationBuilder.DropTable(
                name: "meetings");

            migrationBuilder.DropTable(
                name: "oauth_connections");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "time_entries");

            migrationBuilder.DropTable(
                name: "whatsapp_accounts");

            migrationBuilder.DropTable(
                name: "file_assets");

            migrationBuilder.DropTable(
                name: "communications");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "deals");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "companies");
        }
    }
}
