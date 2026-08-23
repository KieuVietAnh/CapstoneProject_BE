using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentOperationsAndStaffResponsibilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_area_assignments_user_id",
                table: "staff_area_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_incident_subscription_source_type",
                table: "incident_subscriptions");

            migrationBuilder.AddColumn<int>(
                name: "category_id",
                table: "staff_area_assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "incident_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_id",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_type",
                table: "notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_staff_user_id",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "severity",
                table: "incidents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.Sql(
                "UPDATE incidents SET status = 'New' WHERE status IN ('Submitted', 'AiReviewed');");

            migrationBuilder.CreateIndex(
                name: "IX_staff_area_assignments_category_id",
                table: "staff_area_assignments",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "uq_staff_responsibility_scope",
                table: "staff_area_assignments",
                columns: new[] { "user_id", "area_id", "category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_incident_id",
                table: "notifications",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_assigned_staff_user_id",
                table: "incidents",
                column: "assigned_staff_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_incident_severity",
                table: "incidents",
                sql: "severity IN ('Low', 'Medium', 'High', 'Critical')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_incident_subscription_source_type",
                table: "incident_subscriptions",
                sql: "source_type IN ('Report', 'Follow', 'Support', 'Backfill', 'Manual')");

            migrationBuilder.AddForeignKey(
                name: "fk_incident_assigned_staff",
                table: "incidents",
                column: "assigned_staff_user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_incident",
                table: "notifications",
                column: "incident_id",
                principalTable: "incidents",
                principalColumn: "incident_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_staff_area_assignment_category",
                table: "staff_area_assignments",
                column: "category_id",
                principalTable: "urban_service_categories",
                principalColumn: "category_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_incident_assigned_staff",
                table: "incidents");

            migrationBuilder.DropForeignKey(
                name: "fk_notification_incident",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "fk_staff_area_assignment_category",
                table: "staff_area_assignments");

            migrationBuilder.DropIndex(
                name: "IX_staff_area_assignments_category_id",
                table: "staff_area_assignments");

            migrationBuilder.DropIndex(
                name: "uq_staff_responsibility_scope",
                table: "staff_area_assignments");

            migrationBuilder.DropIndex(
                name: "IX_notifications_incident_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_incidents_assigned_staff_user_id",
                table: "incidents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_incident_severity",
                table: "incidents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_incident_subscription_source_type",
                table: "incident_subscriptions");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "staff_area_assignments");

            migrationBuilder.DropColumn(
                name: "incident_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "target_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "target_type",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "assigned_staff_user_id",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "incidents");

            migrationBuilder.CreateIndex(
                name: "IX_staff_area_assignments_user_id",
                table: "staff_area_assignments",
                column: "user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_incident_subscription_source_type",
                table: "incident_subscriptions",
                sql: "source_type IN ('Report', 'Follow', 'Support', 'Backfill')");
        }
    }
}
