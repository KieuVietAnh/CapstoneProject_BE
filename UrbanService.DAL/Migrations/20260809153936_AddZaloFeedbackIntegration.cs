using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddZaloFeedbackIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "zalo_feedback_conversations",
                columns: table => new
                {
                    conversation_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    oa_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sender_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    location_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    area_id = table.Column<int>(type: "integer", nullable: true),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("zalo_feedback_conversations_pkey", x => x.conversation_id);
                    table.ForeignKey(
                        name: "fk_zalo_feedback_conversation_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_zalo_feedback_conversation_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "zalo_oauth_credentials",
                columns: table => new
                {
                    oa_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    access_token_ciphertext = table.Column<string>(type: "text", nullable: false),
                    refresh_token_ciphertext = table.Column<string>(type: "text", nullable: true),
                    access_token_expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("zalo_oauth_credentials_pkey", x => x.oa_id);
                });

            migrationBuilder.CreateTable(
                name: "zalo_webhook_events",
                columns: table => new
                {
                    webhook_event_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    processed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("zalo_webhook_events_pkey", x => x.webhook_event_id);
                });

            migrationBuilder.CreateTable(
                name: "zalo_feedback_draft_attachments",
                columns: table => new
                {
                    draft_attachment_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<long>(type: "bigint", nullable: false),
                    source_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    file_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("zalo_feedback_draft_attachments_pkey", x => x.draft_attachment_id);
                    table.ForeignKey(
                        name: "fk_zalo_feedback_draft_attachment_conversation",
                        column: x => x.conversation_id,
                        principalTable: "zalo_feedback_conversations",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zalo_feedback_submissions",
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
                    table.PrimaryKey("zalo_feedback_submissions_pkey", x => x.submission_id);
                    table.ForeignKey(
                        name: "fk_zalo_feedback_submission_conversation",
                        column: x => x.conversation_id,
                        principalTable: "zalo_feedback_conversations",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_zalo_feedback_submission_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_zalo_feedback_conversations_area_id",
                table: "zalo_feedback_conversations",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "ix_zalo_feedback_conversations_feedback_id",
                table: "zalo_feedback_conversations",
                column: "feedback_id");

            migrationBuilder.CreateIndex(
                name: "uq_zalo_feedback_conversations_oa_sender",
                table: "zalo_feedback_conversations",
                columns: new[] { "oa_id", "sender_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_zalo_feedback_draft_attachments_conversation_created_at",
                table: "zalo_feedback_draft_attachments",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_zalo_feedback_submissions_conversation_created_at",
                table: "zalo_feedback_submissions",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_zalo_feedback_submissions_feedback_id",
                table: "zalo_feedback_submissions",
                column: "feedback_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_zalo_webhook_events_status_received_at",
                table: "zalo_webhook_events",
                columns: new[] { "status", "received_at" });

            migrationBuilder.CreateIndex(
                name: "uq_zalo_webhook_events_event_key",
                table: "zalo_webhook_events",
                column: "event_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "zalo_feedback_draft_attachments");

            migrationBuilder.DropTable(
                name: "zalo_feedback_submissions");

            migrationBuilder.DropTable(
                name: "zalo_oauth_credentials");

            migrationBuilder.DropTable(
                name: "zalo_webhook_events");

            migrationBuilder.DropTable(
                name: "zalo_feedback_conversations");
        }
    }
}
