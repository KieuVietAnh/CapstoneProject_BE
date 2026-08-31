using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class MoveStaffProviderWorkflowToIncident : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE completion_documents
                    DROP CONSTRAINT IF EXISTS fk_completion_document_feedback;
                ALTER TABLE feedback_provider_reports
                    DROP CONSTRAINT IF EXISTS fk_feedback_provider_report_feedback;
                ALTER TABLE feedback_resolutions
                    DROP CONSTRAINT IF EXISTS fk_feedback_resolution_feedback;

                DROP INDEX IF EXISTS "IX_feedback_resolutions_feedback_id";
                DROP INDEX IF EXISTS "IX_feedback_provider_reports_feedback_id";
                DROP INDEX IF EXISTS "IX_completion_documents_feedback_id";
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "incident_id",
                table: "feedback_resolutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "incident_id",
                table: "feedback_provider_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "incident_id",
                table: "completion_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "assigned_at",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE feedback_provider_reports AS assignment
                SET incident_id = link.incident_id
                FROM incident_report_links AS link
                WHERE link.feedback_id = assignment.feedback_id
                  AND link.link_status = 'Active';

                UPDATE feedback_resolutions AS resolution
                SET incident_id = link.incident_id
                FROM incident_report_links AS link
                WHERE link.feedback_id = resolution.feedback_id
                  AND link.link_status = 'Active';

                UPDATE completion_documents AS document
                SET incident_id = assignment.incident_id
                FROM feedback_provider_reports AS assignment
                WHERE assignment.provider_report_id = document.provider_report_id;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM feedback_provider_reports WHERE incident_id IS NULL) OR
                       EXISTS (SELECT 1 FROM feedback_resolutions WHERE incident_id IS NULL) OR
                       EXISTS (SELECT 1 FROM completion_documents WHERE incident_id IS NULL) THEN
                        RAISE EXCEPTION 'Cannot migrate provider workflow: legacy row has no active Incident link.';
                    END IF;

                    IF EXISTS (
                        SELECT incident_id
                        FROM feedback_provider_reports
                        GROUP BY incident_id
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate provider workflow: an Incident has multiple provider assignments.';
                    END IF;

                    IF EXISTS (
                        SELECT incident_id
                        FROM feedback_resolutions
                        GROUP BY incident_id
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate provider workflow: an Incident has multiple resolutions.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "incident_id",
                table: "feedback_resolutions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "incident_id",
                table: "feedback_provider_reports",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "incident_id",
                table: "completion_documents",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "feedback_id",
                table: "feedback_resolutions");

            migrationBuilder.DropColumn(
                name: "feedback_id",
                table: "feedback_provider_reports");

            migrationBuilder.DropColumn(
                name: "feedback_id",
                table: "completion_documents");

            migrationBuilder.AddColumn<DateTime>(
                name: "processing_started_at",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_feedback_resolutions_incident_id",
                table: "feedback_resolutions",
                column: "incident_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_completion_documents_incident_id",
                table: "completion_documents",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "ux_feedback_provider_reports_incident_id",
                table: "feedback_provider_reports",
                column: "incident_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_completion_document_incident",
                table: "completion_documents",
                column: "incident_id",
                principalTable: "incidents",
                principalColumn: "incident_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_provider_report_incident",
                table: "feedback_provider_reports",
                column: "incident_id",
                principalTable: "incidents",
                principalColumn: "incident_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_resolution_incident",
                table: "feedback_resolutions",
                column: "incident_id",
                principalTable: "incidents",
                principalColumn: "incident_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_completion_document_incident",
                table: "completion_documents");

            migrationBuilder.DropForeignKey(
                name: "fk_feedback_provider_report_incident",
                table: "feedback_provider_reports");

            migrationBuilder.DropForeignKey(
                name: "fk_feedback_resolution_incident",
                table: "feedback_resolutions");

            migrationBuilder.DropIndex(
                name: "ux_feedback_resolutions_incident_id",
                table: "feedback_resolutions");

            migrationBuilder.DropIndex(
                name: "ux_feedback_provider_reports_incident_id",
                table: "feedback_provider_reports");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "processing_started_at",
                table: "incidents");

            migrationBuilder.AddColumn<Guid>(
                name: "feedback_id",
                table: "feedback_resolutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "feedback_id",
                table: "feedback_provider_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "feedback_id",
                table: "completion_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE feedback_provider_reports AS assignment
                SET feedback_id = selected.feedback_id
                FROM (
                    SELECT DISTINCT ON (incident_id) incident_id, feedback_id
                    FROM incident_report_links
                    WHERE link_status = 'Active'
                    ORDER BY incident_id,
                             CASE WHEN link_role = 'Primary' THEN 0 ELSE 1 END,
                             linked_at
                ) AS selected
                WHERE selected.incident_id = assignment.incident_id;

                UPDATE feedback_resolutions AS resolution
                SET feedback_id = assignment.feedback_id
                FROM feedback_provider_reports AS assignment
                WHERE assignment.incident_id = resolution.incident_id;

                UPDATE completion_documents AS document
                SET feedback_id = assignment.feedback_id
                FROM feedback_provider_reports AS assignment
                WHERE assignment.provider_report_id = document.provider_report_id;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM feedback_provider_reports WHERE feedback_id IS NULL) OR
                       EXISTS (SELECT 1 FROM feedback_resolutions WHERE feedback_id IS NULL) OR
                       EXISTS (SELECT 1 FROM completion_documents WHERE feedback_id IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back provider workflow: Incident has no active Feedback link.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "feedback_id",
                table: "feedback_resolutions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "feedback_id",
                table: "feedback_provider_reports",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "feedback_id",
                table: "completion_documents",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "incident_id",
                table: "feedback_resolutions");

            migrationBuilder.DropColumn(
                name: "incident_id",
                table: "feedback_provider_reports");

            migrationBuilder.DropColumn(
                name: "incident_id",
                table: "completion_documents");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_resolutions_feedback_id",
                table: "feedback_resolutions",
                column: "feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_provider_reports_feedback_id",
                table: "feedback_provider_reports",
                column: "feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_completion_documents_feedback_id",
                table: "completion_documents",
                column: "feedback_id");

            migrationBuilder.AddForeignKey(
                name: "fk_completion_document_feedback",
                table: "completion_documents",
                column: "feedback_id",
                principalTable: "feedbacks",
                principalColumn: "feedback_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_provider_report_feedback",
                table: "feedback_provider_reports",
                column: "feedback_id",
                principalTable: "feedbacks",
                principalColumn: "feedback_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_resolution_feedback",
                table: "feedback_resolutions",
                column: "feedback_id",
                principalTable: "feedbacks",
                principalColumn: "feedback_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
