using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentResolutionReviewMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "review_reason",
                table: "feedback_resolutions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "feedback_resolutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_manager_id",
                table: "feedback_resolutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_feedback_resolutions_reviewed_by_manager_id",
                table: "feedback_resolutions",
                column: "reviewed_by_manager_id");

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_resolution_reviewer",
                table: "feedback_resolutions",
                column: "reviewed_by_manager_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_feedback_resolution_reviewer",
                table: "feedback_resolutions");

            migrationBuilder.DropIndex(
                name: "IX_feedback_resolutions_reviewed_by_manager_id",
                table: "feedback_resolutions");

            migrationBuilder.DropColumn(
                name: "review_reason",
                table: "feedback_resolutions");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "feedback_resolutions");

            migrationBuilder.DropColumn(
                name: "reviewed_by_manager_id",
                table: "feedback_resolutions");
        }
    }
}
