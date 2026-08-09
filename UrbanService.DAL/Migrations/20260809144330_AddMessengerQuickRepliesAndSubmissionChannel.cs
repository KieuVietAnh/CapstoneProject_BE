using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMessengerQuickRepliesAndSubmissionChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "submission_channel",
                table: "feedbacks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Web");

            migrationBuilder.CreateTable(
                name: "messenger_feedback_submissions",
                columns: table => new
                {
                    submission_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<long>(type: "bigint", nullable: false),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("messenger_feedback_submissions_pkey", x => x.submission_id);
                    table.ForeignKey(
                        name: "fk_messenger_feedback_submission_conversation",
                        column: x => x.conversation_id,
                        principalTable: "messenger_feedback_conversations",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_messenger_feedback_submission_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_messenger_feedback_submissions_conversation_created_at",
                table: "messenger_feedback_submissions",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_messenger_feedback_submissions_feedback_id",
                table: "messenger_feedback_submissions",
                column: "feedback_id",
                unique: true);

            migrationBuilder.Sql(
                """
                UPDATE feedbacks
                SET submission_channel = 'Messenger'
                WHERE LOWER(COALESCE(geo_source, '')) = 'messenger';
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO messenger_feedback_submissions
                    (conversation_id, feedback_id, created_at)
                SELECT conversation_id, feedback_id, updated_at
                FROM messenger_feedback_conversations
                WHERE feedback_id IS NOT NULL
                ON CONFLICT (feedback_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messenger_feedback_submissions");

            migrationBuilder.DropColumn(
                name: "submission_channel",
                table: "feedbacks");
        }
    }
}
