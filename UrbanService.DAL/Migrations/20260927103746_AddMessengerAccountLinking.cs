using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMessengerAccountLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "messenger_account_links",
                columns: table => new
                {
                    link_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    page_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sender_psid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    linked_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messenger_account_links_pkey", x => x.link_id);
                    table.CheckConstraint("ck_messenger_account_links_active_revoked", "(is_active = TRUE AND revoked_at IS NULL) OR (is_active = FALSE AND revoked_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_messenger_account_link_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messenger_link_tokens",
                columns: table => new
                {
                    link_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sender_psid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    invalidated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messenger_link_tokens_pkey", x => x.link_token_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_messenger_account_links_user_id",
                table: "messenger_account_links",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_messenger_account_links_active_page_sender",
                table: "messenger_account_links",
                columns: new[] { "page_id", "sender_psid" },
                unique: true,
                filter: "is_active = TRUE");

            migrationBuilder.CreateIndex(
                name: "uq_messenger_account_links_active_page_user",
                table: "messenger_account_links",
                columns: new[] { "page_id", "user_id" },
                unique: true,
                filter: "is_active = TRUE");

            migrationBuilder.CreateIndex(
                name: "ix_messenger_link_tokens_page_sender_expires_at",
                table: "messenger_link_tokens",
                columns: new[] { "page_id", "sender_psid", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "uq_messenger_link_tokens_hash",
                table: "messenger_link_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messenger_account_links");

            migrationBuilder.DropTable(
                name: "messenger_link_tokens");
        }
    }
}
