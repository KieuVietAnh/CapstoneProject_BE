using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMessengerFeedbackDraftAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "messenger_feedback_draft_attachments",
                columns: table => new
                {
                    draft_attachment_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<long>(type: "bigint", nullable: false),
                    source_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    file_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_ordinal = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("messenger_feedback_draft_attachments_pkey", x => x.draft_attachment_id);
                    table.ForeignKey(
                        name: "fk_messenger_feedback_draft_attachment_conversation",
                        column: x => x.conversation_id,
                        principalTable: "messenger_feedback_conversations",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_messenger_feedback_draft_attachments_conversation_created_at",
                table: "messenger_feedback_draft_attachments",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "uq_messenger_feedback_draft_attachments_message_ordinal",
                table: "messenger_feedback_draft_attachments",
                columns: new[] { "conversation_id", "source_message_id", "source_ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messenger_feedback_draft_attachments");
        }
    }
}
