using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.CreateTable(
                name: "sla_policies",
                columns: table => new
                {
                    sla_policy_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    policy_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: true),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    response_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    resolution_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sla_policies_pkey", x => x.sla_policy_id);
                    table.ForeignKey(
                        name: "fk_sla_policies_categories",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sla_policies_created_by_user",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sla_policies_operating_areas",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sla_policies_updated_by_user",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "feedback_slas",
                columns: table => new
                {
                    feedback_sla_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sla_policy_id = table.Column<int>(type: "integer", nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    response_due_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    resolution_due_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    total_paused_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    response_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    resolution_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_response_breached = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_resolution_breached = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    started_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("feedback_slas_pkey", x => x.feedback_sla_id);
                    table.ForeignKey(
                        name: "fk_feedback_slas_categories",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feedback_slas_completed_by_user",
                        column: x => x.completed_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feedback_slas_feedbacks",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_feedback_slas_operating_areas",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feedback_slas_sla_policies",
                        column: x => x.sla_policy_id,
                        principalTable: "sla_policies",
                        principalColumn: "sla_policy_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feedback_slas_started_by_user",
                        column: x => x.started_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_events",
                columns: table => new
                {
                    sla_event_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    feedback_sla_id = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    old_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    new_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    triggered_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trigger_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sla_events_pkey", x => x.sla_event_id);
                    table.ForeignKey(
                        name: "fk_sla_events_feedback_slas",
                        column: x => x.feedback_sla_id,
                        principalTable: "feedback_slas",
                        principalColumn: "feedback_sla_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sla_events_triggered_by_user",
                        column: x => x.triggered_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_pause_histories",
                columns: table => new
                {
                    sla_pause_history_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    feedback_sla_id = table.Column<long>(type: "bigint", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    paused_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    resumed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    paused_minutes = table.Column<int>(type: "integer", nullable: true),
                    paused_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resumed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("sla_pause_histories_pkey", x => x.sla_pause_history_id);
                    table.ForeignKey(
                        name: "fk_sla_pause_histories_feedback_slas",
                        column: x => x.feedback_sla_id,
                        principalTable: "feedback_slas",
                        principalColumn: "feedback_sla_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sla_pause_histories_paused_by_user",
                        column: x => x.paused_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sla_pause_histories_resumed_by_user",
                        column: x => x.resumed_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feedback_slas_area_id",
                table: "feedback_slas",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_slas_category_id",
                table: "feedback_slas",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_slas_completed_by_user_id",
                table: "feedback_slas",
                column: "completed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_slas_monitoring",
                table: "feedback_slas",
                columns: new[] { "status", "response_due_at", "resolution_due_at" });

            migrationBuilder.CreateIndex(
                name: "IX_feedback_slas_sla_policy_id",
                table: "feedback_slas",
                column: "sla_policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_slas_started_by_user_id",
                table: "feedback_slas",
                column: "started_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_feedback_slas_current_feedback",
                table: "feedback_slas",
                column: "feedback_id",
                unique: true,
                filter: "is_current = true");

            migrationBuilder.CreateIndex(
                name: "ix_sla_events_feedback_sla_created_at",
                table: "sla_events",
                columns: new[] { "feedback_sla_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_events_triggered_by_user_id",
                table: "sla_events",
                column: "triggered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sla_pause_histories_feedback_sla_id",
                table: "sla_pause_histories",
                column: "feedback_sla_id");

            migrationBuilder.CreateIndex(
                name: "IX_sla_pause_histories_paused_by_user_id",
                table: "sla_pause_histories",
                column: "paused_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sla_pause_histories_resumed_by_user_id",
                table: "sla_pause_histories",
                column: "resumed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sla_policies_category_id",
                table: "sla_policies",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_sla_policies_created_by_user_id",
                table: "sla_policies",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sla_policies_lookup",
                table: "sla_policies",
                columns: new[] { "area_id", "category_id", "priority", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_sla_policies_updated_by_user_id",
                table: "sla_policies",
                column: "updated_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sla_events");

            migrationBuilder.DropTable(
                name: "sla_pause_histories");

            migrationBuilder.DropTable(
                name: "feedback_slas");

            migrationBuilder.DropTable(
                name: "sla_policies");

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
                name: "IX_channels_feedback_id",
                table: "channels",
                column: "feedback_id");
        }
    }
}
