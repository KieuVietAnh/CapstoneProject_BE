using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackDuplicateCandidate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feedback_duplicate_candidates",
                columns: table => new
                {
                    duplicate_candidate_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    potential_parent_feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Pending'::character varying"),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("feedback_duplicate_candidates_pkey", x => x.duplicate_candidate_id);
                    table.ForeignKey(
                        name: "fk_feedback_duplicate_candidate_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_feedback_duplicate_candidate_parent_feedback",
                        column: x => x.potential_parent_feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feedback_duplicate_candidate_reviewed_by",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feedback_duplicate_candidates_potential_parent_feedback_id",
                table: "feedback_duplicate_candidates",
                column: "potential_parent_feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_duplicate_candidates_reviewed_by_user_id",
                table: "feedback_duplicate_candidates",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_duplicate_candidates_status",
                table: "feedback_duplicate_candidates",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_feedback_duplicate_candidate_pair",
                table: "feedback_duplicate_candidates",
                columns: new[] { "feedback_id", "potential_parent_feedback_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feedback_duplicate_candidates");
        }
    }
}
