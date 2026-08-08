using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    createdBy = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflows_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workflows_users_createdBy",
                        column: x => x.createdBy,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "workflow_actions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    workflowId = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    orderIndex = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_actions_workflows_workflowId",
                        column: x => x.workflowId,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_condition_groups",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    workflowId = table.Column<string>(type: "text", nullable: false),
                    parentGroupId = table.Column<string>(type: "text", nullable: true),
                    logicOperator = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_condition_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_condition_groups_workflow_condition_groups_parentG~",
                        column: x => x.parentGroupId,
                        principalTable: "workflow_condition_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workflow_condition_groups_workflows_workflowId",
                        column: x => x.workflowId,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_executions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    workflowId = table.Column<string>(type: "text", nullable: false),
                    triggeredBy = table.Column<int>(type: "integer", nullable: false),
                    triggerContext = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    startedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_executions_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workflow_executions_workflows_workflowId",
                        column: x => x.workflowId,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_triggers",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    companyId = table.Column<string>(type: "text", nullable: false),
                    workflowId = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: true),
                    cronExpression = table.Column<string>(type: "text", nullable: true),
                    nextRunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_triggers", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_triggers_companies_companyId",
                        column: x => x.companyId,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workflow_triggers_workflows_workflowId",
                        column: x => x.workflowId,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_conditions",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    conditionGroupId = table.Column<string>(type: "text", nullable: false),
                    field = table.Column<string>(type: "text", nullable: false),
                    @operator = table.Column<int>(name: "operator", type: "integer", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_conditions", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_conditions_workflow_condition_groups_conditionGrou~",
                        column: x => x.conditionGroupId,
                        principalTable: "workflow_condition_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_execution_steps",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    workflowExecutionId = table.Column<string>(type: "text", nullable: false),
                    actionId = table.Column<string>(type: "text", nullable: true),
                    actionType = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    output = table.Column<string>(type: "jsonb", nullable: true),
                    error = table.Column<string>(type: "jsonb", nullable: true),
                    executedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_execution_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_execution_steps_workflow_actions_actionId",
                        column: x => x.actionId,
                        principalTable: "workflow_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_workflow_execution_steps_workflow_executions_workflowExecut~",
                        column: x => x.workflowExecutionId,
                        principalTable: "workflow_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_actions_workflowId_orderIndex",
                table: "workflow_actions",
                columns: new[] { "workflowId", "orderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_condition_groups_parentGroupId",
                table: "workflow_condition_groups",
                column: "parentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_condition_groups_workflowId",
                table: "workflow_condition_groups",
                column: "workflowId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_conditions_conditionGroupId",
                table: "workflow_conditions",
                column: "conditionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_execution_steps_actionId",
                table: "workflow_execution_steps",
                column: "actionId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_execution_steps_workflowExecutionId",
                table: "workflow_execution_steps",
                column: "workflowExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_executions_companyId_startedAt",
                table: "workflow_executions",
                columns: new[] { "companyId", "startedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_executions_workflowId_startedAt",
                table: "workflow_executions",
                columns: new[] { "workflowId", "startedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_triggers_companyId",
                table: "workflow_triggers",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_triggers_type_nextRunAtUtc",
                table: "workflow_triggers",
                columns: new[] { "type", "nextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_triggers_workflowId",
                table: "workflow_triggers",
                column: "workflowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflows_companyId_isActive",
                table: "workflows",
                columns: new[] { "companyId", "isActive" });

            migrationBuilder.CreateIndex(
                name: "IX_workflows_createdBy",
                table: "workflows",
                column: "createdBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_conditions");

            migrationBuilder.DropTable(
                name: "workflow_execution_steps");

            migrationBuilder.DropTable(
                name: "workflow_triggers");

            migrationBuilder.DropTable(
                name: "workflow_condition_groups");

            migrationBuilder.DropTable(
                name: "workflow_actions");

            migrationBuilder.DropTable(
                name: "workflow_executions");

            migrationBuilder.DropTable(
                name: "workflows");
        }
    }
}
