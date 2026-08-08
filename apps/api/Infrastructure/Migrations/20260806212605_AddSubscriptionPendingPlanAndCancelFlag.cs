using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPendingPlanAndCancelFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cancelAtPeriodEnd",
                table: "subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "pendingPlanId",
                table: "subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_pendingPlanId",
                table: "subscriptions",
                column: "pendingPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_subscription_plans_pendingPlanId",
                table: "subscriptions",
                column: "pendingPlanId",
                principalTable: "subscription_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_subscription_plans_pendingPlanId",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_pendingPlanId",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "cancelAtPeriodEnd",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "pendingPlanId",
                table: "subscriptions");
        }
    }
}
