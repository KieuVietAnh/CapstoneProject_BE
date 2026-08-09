using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMessengerFeedbackConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "messenger_feedback_conversations",
                columns: table => new
                {
                    conversation_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    page_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sender_psid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    location_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    area_id = table.Column<int>(type: "integer", nullable: true),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messenger_feedback_conversations_pkey", x => x.conversation_id);
                    table.ForeignKey(
                        name: "fk_messenger_feedback_conversation_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_messenger_feedback_conversation_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_messenger_feedback_conversations_area_id",
                table: "messenger_feedback_conversations",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "ix_messenger_feedback_conversations_feedback_id",
                table: "messenger_feedback_conversations",
                column: "feedback_id");

            migrationBuilder.CreateIndex(
                name: "uq_messenger_feedback_conversations_page_sender",
                table: "messenger_feedback_conversations",
                columns: new[] { "page_id", "sender_psid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messenger_feedback_conversations");
        }
    }
}
