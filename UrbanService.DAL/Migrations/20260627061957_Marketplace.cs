using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Marketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_service_payment_service",
                table: "service_payments");

            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropIndex(
                name: "IX_service_payments_service_id",
                table: "service_payments");

            migrationBuilder.DropColumn(
                name: "service_id",
                table: "service_payments");

            migrationBuilder.AddColumn<Guid>(
                name: "booking_id",
                table: "service_payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    booking_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_service_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contact_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    service_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    schedule_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValueSql: "'VND'::character varying"),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("bookings_pkey", x => x.booking_id);
                    table.ForeignKey(
                        name: "fk_booking_service_staff",
                        column: x => x.assigned_service_staff_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_booking_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_assignment_histories",
                columns: table => new
                {
                    history_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_service_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_service_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("booking_assignment_histories_pkey", x => x.history_id);
                    table.ForeignKey(
                        name: "FK_booking_assignment_histories_users_assigned_by_user_id",
                        column: x => x.assigned_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_assignment_histories_users_new_service_staff_id",
                        column: x => x.new_service_staff_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_assignment_histories_users_old_service_staff_id",
                        column: x => x.old_service_staff_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_booking_assignment_history_booking",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_details",
                columns: table => new
                {
                    booking_detail_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("booking_details_pkey", x => x.booking_detail_id);
                    table.ForeignKey(
                        name: "fk_booking_detail_booking",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_detail_service",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "service_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_executions",
                columns: table => new
                {
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    executed_by_service_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    action_taken = table.Column<string>(type: "text", nullable: false),
                    result_note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_executions_pkey", x => x.execution_id);
                    table.ForeignKey(
                        name: "fk_service_execution_booking",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_service_execution_staff",
                        column: x => x.executed_by_service_staff_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_reviews",
                columns: table => new
                {
                    review_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    is_satisfied = table.Column<bool>(type: "boolean", nullable: true),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_reviews_pkey", x => x.review_id);
                    table.ForeignKey(
                        name: "fk_service_review_booking",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_service_review_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_execution_attachments",
                columns: table => new
                {
                    service_execution_attachment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_execution_attachments_pkey", x => x.service_execution_attachment_id);
                    table.ForeignKey(
                        name: "fk_service_execution_attachment",
                        column: x => x.execution_id,
                        principalTable: "service_executions",
                        principalColumn: "execution_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_payments_booking_id",
                table: "service_payments",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_assignment_histories_assigned_by_user_id",
                table: "booking_assignment_histories",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_assignment_histories_booking_id",
                table: "booking_assignment_histories",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_assignment_histories_new_service_staff_id",
                table: "booking_assignment_histories",
                column: "new_service_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_assignment_histories_old_service_staff_id",
                table: "booking_assignment_histories",
                column: "old_service_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_details_booking_id",
                table: "booking_details",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_details_service_id",
                table: "booking_details",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_assigned_service_staff_id",
                table: "bookings",
                column: "assigned_service_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_booking_code",
                table: "bookings",
                column: "booking_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_user_id",
                table: "bookings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_execution_attachments_execution_id",
                table: "service_execution_attachments",
                column: "execution_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_executions_booking_id",
                table: "service_executions",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_executions_executed_by_service_staff_id",
                table: "service_executions",
                column: "executed_by_service_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_reviews_booking_id",
                table: "service_reviews",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_reviews_user_id",
                table: "service_reviews",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_service_payment_booking",
                table: "service_payments",
                column: "booking_id",
                principalTable: "bookings",
                principalColumn: "booking_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_service_payment_booking",
                table: "service_payments");

            migrationBuilder.DropTable(
                name: "booking_assignment_histories");

            migrationBuilder.DropTable(
                name: "booking_details");

            migrationBuilder.DropTable(
                name: "service_execution_attachments");

            migrationBuilder.DropTable(
                name: "service_reviews");

            migrationBuilder.DropTable(
                name: "service_executions");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_service_payments_booking_id",
                table: "service_payments");

            migrationBuilder.DropColumn(
                name: "booking_id",
                table: "service_payments");

            migrationBuilder.AddColumn<int>(
                name: "service_id",
                table: "service_payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    channel_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    external_conversation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    external_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    source_user_external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("channels_pkey", x => x.channel_id);
                    table.ForeignKey(
                        name: "fk_channel_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_payments_service_id",
                table: "service_payments",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_channels_feedback_id",
                table: "channels",
                column: "feedback_id");

            migrationBuilder.AddForeignKey(
                name: "fk_service_payment_service",
                table: "service_payments",
                column: "service_id",
                principalTable: "services",
                principalColumn: "service_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
