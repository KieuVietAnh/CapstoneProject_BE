using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentCommentsSupports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incident_comments",
                columns: table => new
                {
                    incident_comment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    source_feedback_comment_id = table.Column<int>(type: "integer", nullable: true),
                    source_feedback_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("incident_comments_pkey", x => x.incident_comment_id);
                    table.ForeignKey(
                        name: "fk_incident_comment_incident",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "incident_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_incident_comment_source_feedback",
                        column: x => x.source_feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_incident_comment_source_feedback_comment",
                        column: x => x.source_feedback_comment_id,
                        principalTable: "feedback_comments",
                        principalColumn: "comment_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_incident_comment_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incident_supports",
                columns: table => new
                {
                    incident_support_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("incident_supports_pkey", x => x.incident_support_id);
                    table.ForeignKey(
                        name: "fk_incident_support_incident",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "incident_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_incident_support_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incident_comments_incident_created_at",
                table: "incident_comments",
                columns: new[] { "incident_id", "created_at", "incident_comment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_comments_source_feedback_id",
                table: "incident_comments",
                column: "source_feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_comments_user_id",
                table: "incident_comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_incident_comments_source_feedback_comment",
                table: "incident_comments",
                column: "source_feedback_comment_id",
                unique: true,
                filter: "source_feedback_comment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_incident_supports_user_id",
                table: "incident_supports",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_incident_support_user",
                table: "incident_supports",
                columns: new[] { "incident_id", "user_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO incident_comments (
                    incident_comment_id,
                    incident_id,
                    user_id,
                    content,
                    source_feedback_comment_id,
                    source_feedback_id,
                    created_at)
                SELECT
                    gen_random_uuid(),
                    links.incident_id,
                    comments.user_id,
                    comments.content,
                    comments.comment_id,
                    comments.feedback_id,
                    comments.created_at
                FROM feedback_comments AS comments
                INNER JOIN incident_report_links AS links
                    ON links.feedback_id = comments.feedback_id
                    AND links.link_status = 'Active'
                INNER JOIN feedbacks AS feedbacks
                    ON feedbacks.feedback_id = comments.feedback_id
                INNER JOIN incidents AS incidents
                    ON incidents.incident_id = links.incident_id
                WHERE feedbacks.status NOT IN ('Submitted', 'AiReviewed')
                    AND incidents.merged_into_incident_id IS NULL;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO incident_supports (
                    incident_support_id,
                    incident_id,
                    user_id,
                    created_at)
                SELECT
                    gen_random_uuid(),
                    links.incident_id,
                    supports.user_id,
                    MIN(supports.created_at)
                FROM feedback_supports AS supports
                INNER JOIN incident_report_links AS links
                    ON links.feedback_id = supports.feedback_id
                    AND links.link_status = 'Active'
                INNER JOIN feedbacks AS feedbacks
                    ON feedbacks.feedback_id = supports.feedback_id
                INNER JOIN incidents AS incidents
                    ON incidents.incident_id = links.incident_id
                WHERE feedbacks.status NOT IN ('Submitted', 'AiReviewed')
                    AND incidents.merged_into_incident_id IS NULL
                GROUP BY links.incident_id, supports.user_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_comments");

            migrationBuilder.DropTable(
                name: "incident_supports");
        }
    }
}
