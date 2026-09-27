using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneVerificationAndOtpQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "firebase_uid",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "phone_verified_at",
                table: "users",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "phone_otp_requests",
                columns: table => new
                {
                    phone_otp_request_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    day = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("phone_otp_requests_pkey", x => x.phone_otp_request_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_phone_otp_requests_day",
                table: "phone_otp_requests",
                column: "day");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "phone_otp_requests");

            migrationBuilder.DropColumn(
                name: "firebase_uid",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone_verified_at",
                table: "users");
        }
    }
}
