using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AreaAlertContractSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE IF EXISTS feedback_resolutions DROP CONSTRAINT IF EXISTS fk_feedback_resolution_operator;");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS feedback_status_histories DROP CONSTRAINT IF EXISTS fk_status_history_feedback;");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS feedback_status_histories DROP CONSTRAINT IF EXISTS fk_status_history_user;");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS message_attachments DROP CONSTRAINT IF EXISTS fk_message_attachment_interaction_message;");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS users DROP CONSTRAINT IF EXISTS fk_user_operator;");

            migrationBuilder.Sql("DROP TABLE IF EXISTS booking_assignment_histories CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS booking_details CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS feedback_assignments CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS feedback_resolution_attachments CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS service_execution_attachments CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS service_payments CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS service_reviews CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS services CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS service_executions CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS service_operators CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS bookings CASCADE;");

            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_users_operator_id\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_feedback_resolutions_operator_id\";");

            migrationBuilder.Sql("ALTER TABLE IF EXISTS users DROP COLUMN IF EXISTS operator_id;");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS feedback_resolutions DROP COLUMN IF EXISTS operator_id;");

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'feedback_resolutions'
                          AND column_name = 'resolved_by_user_id'
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'feedback_resolutions'
                          AND column_name = 'created_by_staff_user_id'
                    ) THEN
                        ALTER TABLE feedback_resolutions
                            RENAME COLUMN resolved_by_user_id TO created_by_staff_user_id;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('"IX_feedback_resolutions_resolved_by_user_id"') IS NOT NULL
                       AND to_regclass('"IX_feedback_resolutions_created_by_staff_user_id"') IS NULL THEN
                        ALTER INDEX "IX_feedback_resolutions_resolved_by_user_id"
                            RENAME TO "IX_feedback_resolutions_created_by_staff_user_id";
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<int>(
                name: "alert_id",
                table: "notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "area_id",
                table: "feedbacks",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "geo_source",
                table: "feedbacks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_location_verified",
                table: "feedbacks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "location_accuracy_meters",
                table: "feedbacks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "provider_report_id",
                table: "feedback_resolutions",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "raw_response",
                table: "analysis_results",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "detected_area_id",
                table: "analysis_results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "area_id",
                table: "ai_knowledge_sources",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    audit_log_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("audit_logs_pkey", x => x.audit_log_id);
                    table.ForeignKey(
                        name: "fk_audit_log_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                    source_user_external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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

            migrationBuilder.CreateTable(
                name: "operating_areas",
                columns: table => new
                {
                    area_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ward_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    district_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    province_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    center_latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    center_longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    boundary_geo_json = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    started_at = table.Column<DateOnly>(type: "date", nullable: true),
                    ended_at = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("operating_areas_pkey", x => x.area_id);
                });

            migrationBuilder.InsertData(
                table: "operating_areas",
                columns: new[] { "area_id", "area_name", "area_type", "is_active", "created_at" },
                values: new object[] { 1, "Unassigned", "CustomArea", true, new DateTime(2026, 7, 4, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('operating_areas', 'area_id'), GREATEST((SELECT MAX(area_id) FROM operating_areas), 1));");

            migrationBuilder.CreateTable(
                name: "service_provider_coordinators",
                columns: table => new
                {
                    coordinator_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    coordinator_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_provider_coordinators_pkey", x => x.coordinator_id);
                });

            migrationBuilder.CreateTable(
                name: "area_hotspots",
                columns: table => new
                {
                    hotspot_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    center_latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    center_longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    radius_meters = table.Column<int>(type: "integer", nullable: true),
                    time_window_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    time_window_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    feedback_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    master_ticket_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    average_priority_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    risk_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Active'::character varying"),
                    detected_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_query_json = table.Column<string>(type: "text", nullable: true),
                    first_detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("area_hotspots_pkey", x => x.hotspot_id);
                    table.ForeignKey(
                        name: "fk_area_hotspot_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_area_hotspot_category",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_area_assignments",
                columns: table => new
                {
                    staff_area_assignment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("staff_area_assignments_pkey", x => x.staff_area_assignment_id);
                    table.ForeignKey(
                        name: "fk_staff_area_assignment_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_staff_area_assignment_assigned_by",
                        column: x => x.assigned_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_staff_area_assignment_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_area_subscriptions",
                columns: table => new
                {
                    subscription_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    is_primary_area = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    receive_alerts = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_area_subscriptions_pkey", x => x.subscription_id);
                    table.ForeignKey(
                        name: "fk_user_area_subscription_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_area_subscription_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "coordinator_coverages",
                columns: table => new
                {
                    coverage_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    coordinator_id = table.Column<int>(type: "integer", nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    priority_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("coordinator_coverages_pkey", x => x.coverage_id);
                    table.ForeignKey(
                        name: "fk_coordinator_coverage_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_coordinator_coverage_category",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_coordinator_coverage_coordinator",
                        column: x => x.coordinator_id,
                        principalTable: "service_provider_coordinators",
                        principalColumn: "coordinator_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feedback_provider_reports",
                columns: table => new
                {
                    provider_report_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coordinator_id = table.Column<int>(type: "integer", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Reported'::character varying"),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    report_note = table.Column<string>(type: "text", nullable: true),
                    reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("feedback_provider_reports_pkey", x => x.provider_report_id);
                    table.ForeignKey(
                        name: "fk_feedback_provider_report_coordinator",
                        column: x => x.coordinator_id,
                        principalTable: "service_provider_coordinators",
                        principalColumn: "coordinator_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feedback_provider_report_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_feedback_provider_report_user",
                        column: x => x.reported_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_contracts",
                columns: table => new
                {
                    contract_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    coordinator_id = table.Column<int>(type: "integer", nullable: false),
                    area_id = table.Column<int>(type: "integer", nullable: true),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    contract_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contract_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Draft'::character varying"),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("provider_contracts_pkey", x => x.contract_id);
                    table.ForeignKey(
                        name: "fk_provider_contract_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_provider_contract_category",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_provider_contract_coordinator",
                        column: x => x.coordinator_id,
                        principalTable: "service_provider_coordinators",
                        principalColumn: "coordinator_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_provider_contract_created_by",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "area_alerts",
                columns: table => new
                {
                    alert_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    hotspot_id = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    alert_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    radius_meters = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Active'::character varying"),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("area_alerts_pkey", x => x.alert_id);
                    table.ForeignKey(
                        name: "fk_area_alert_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_area_alert_category",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_area_alert_hotspot",
                        column: x => x.hotspot_id,
                        principalTable: "area_hotspots",
                        principalColumn: "hotspot_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_area_alert_user",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "completion_documents",
                columns: table => new
                {
                    completion_document_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider_report_id = table.Column<int>(type: "integer", nullable: false),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coordinator_id = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("completion_documents_pkey", x => x.completion_document_id);
                    table.ForeignKey(
                        name: "fk_completion_document_coordinator",
                        column: x => x.coordinator_id,
                        principalTable: "service_provider_coordinators",
                        principalColumn: "coordinator_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_completion_document_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_completion_document_report",
                        column: x => x.provider_report_id,
                        principalTable: "feedback_provider_reports",
                        principalColumn: "provider_report_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_completion_document_uploaded_by",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_contact_logs",
                columns: table => new
                {
                    contact_log_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider_report_id = table.Column<int>(type: "integer", nullable: false),
                    coordinator_id = table.Column<int>(type: "integer", nullable: false),
                    contacted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contact_result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    contact_note = table.Column<string>(type: "text", nullable: true),
                    contacted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("provider_contact_logs_pkey", x => x.contact_log_id);
                    table.ForeignKey(
                        name: "fk_provider_contact_log_coordinator",
                        column: x => x.coordinator_id,
                        principalTable: "service_provider_coordinators",
                        principalColumn: "coordinator_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_provider_contact_log_report",
                        column: x => x.provider_report_id,
                        principalTable: "feedback_provider_reports",
                        principalColumn: "provider_report_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_provider_contact_log_user",
                        column: x => x.contacted_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_contract_attachments",
                columns: table => new
                {
                    contract_attachment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contract_id = table.Column<int>(type: "integer", nullable: false),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("provider_contract_attachments_pkey", x => x.contract_attachment_id);
                    table.ForeignKey(
                        name: "fk_provider_contract_attachment_contract",
                        column: x => x.contract_id,
                        principalTable: "provider_contracts",
                        principalColumn: "contract_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_provider_contract_attachment_uploaded_by",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_alert_id",
                table: "notifications",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedbacks_area_id",
                table: "feedbacks",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_resolutions_provider_report_id",
                table: "feedback_resolutions",
                column: "provider_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_results_detected_area_id",
                table: "analysis_results",
                column: "detected_area_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_sources_area_id",
                table: "ai_knowledge_sources",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_alerts_area_id",
                table: "area_alerts",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_alerts_category_id",
                table: "area_alerts",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_alerts_created_by_user_id",
                table: "area_alerts",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_alerts_hotspot_id",
                table: "area_alerts",
                column: "hotspot_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_hotspots_area_id",
                table: "area_hotspots",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_area_hotspots_category_id",
                table: "area_hotspots",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_channels_feedback_id",
                table: "channels",
                column: "feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_completion_documents_coordinator_id",
                table: "completion_documents",
                column: "coordinator_id");

            migrationBuilder.CreateIndex(
                name: "IX_completion_documents_feedback_id",
                table: "completion_documents",
                column: "feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_completion_documents_provider_report_id",
                table: "completion_documents",
                column: "provider_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_completion_documents_uploaded_by_user_id",
                table: "completion_documents",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_coordinator_coverages_area_id",
                table: "coordinator_coverages",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_coordinator_coverages_category_id",
                table: "coordinator_coverages",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_coordinator_coverages_coordinator_id",
                table: "coordinator_coverages",
                column: "coordinator_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_provider_reports_coordinator_id",
                table: "feedback_provider_reports",
                column: "coordinator_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_provider_reports_feedback_id",
                table: "feedback_provider_reports",
                column: "feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_provider_reports_reported_by_user_id",
                table: "feedback_provider_reports",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "operating_areas_ward_code_key",
                table: "operating_areas",
                column: "ward_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_contact_logs_contacted_by_user_id",
                table: "provider_contact_logs",
                column: "contacted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_contact_logs_coordinator_id",
                table: "provider_contact_logs",
                column: "coordinator_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_contact_logs_provider_report_id",
                table: "provider_contact_logs",
                column: "provider_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_contract_attachments_contract_id",
                table: "provider_contract_attachments",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_contract_attachments_uploaded_by_user_id",
                table: "provider_contract_attachments",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_contracts_area_id",
                table: "provider_contracts",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_contracts_category_id",
                table: "provider_contracts",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_contracts_coordinator_id",
                table: "provider_contracts",
                column: "coordinator_id");

            migrationBuilder.CreateIndex(
                name: "IX_provider_contracts_created_by_user_id",
                table: "provider_contracts",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "provider_contracts_contract_code_key",
                table: "provider_contracts",
                column: "contract_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_area_assignments_area_id",
                table: "staff_area_assignments",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_area_assignments_assigned_by_user_id",
                table: "staff_area_assignments",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_area_assignments_user_id",
                table: "staff_area_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_area_subscriptions_area_id",
                table: "user_area_subscriptions",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_area_subscription",
                table: "user_area_subscriptions",
                columns: new[] { "user_id", "area_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_source_area",
                table: "ai_knowledge_sources",
                column: "area_id",
                principalTable: "operating_areas",
                principalColumn: "area_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_analysis_result_area",
                table: "analysis_results",
                column: "detected_area_id",
                principalTable: "operating_areas",
                principalColumn: "area_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_resolution_report",
                table: "feedback_resolutions",
                column: "provider_report_id",
                principalTable: "feedback_provider_reports",
                principalColumn: "provider_report_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_status_history_feedback",
                table: "feedback_status_histories",
                column: "feedback_id",
                principalTable: "feedbacks",
                principalColumn: "feedback_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_status_history_user",
                table: "feedback_status_histories",
                column: "changed_by_user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_area",
                table: "feedbacks",
                column: "area_id",
                principalTable: "operating_areas",
                principalColumn: "area_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_message_attachment_message",
                table: "message_attachments",
                column: "interaction_message_id",
                principalTable: "interaction_messages",
                principalColumn: "interaction_message_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_area_alert",
                table: "notifications",
                column: "alert_id",
                principalTable: "area_alerts",
                principalColumn: "alert_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_source_area",
                table: "ai_knowledge_sources");

            migrationBuilder.DropForeignKey(
                name: "fk_analysis_result_area",
                table: "analysis_results");

            migrationBuilder.DropForeignKey(
                name: "fk_feedback_resolution_report",
                table: "feedback_resolutions");

            migrationBuilder.DropForeignKey(
                name: "fk_feedback_status_history_feedback",
                table: "feedback_status_histories");

            migrationBuilder.DropForeignKey(
                name: "fk_feedback_status_history_user",
                table: "feedback_status_histories");

            migrationBuilder.DropForeignKey(
                name: "fk_feedback_area",
                table: "feedbacks");

            migrationBuilder.DropForeignKey(
                name: "fk_message_attachment_message",
                table: "message_attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_notification_area_alert",
                table: "notifications");

            migrationBuilder.DropTable(
                name: "area_alerts");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "completion_documents");

            migrationBuilder.DropTable(
                name: "coordinator_coverages");

            migrationBuilder.DropTable(
                name: "provider_contact_logs");

            migrationBuilder.DropTable(
                name: "provider_contract_attachments");

            migrationBuilder.DropTable(
                name: "staff_area_assignments");

            migrationBuilder.DropTable(
                name: "user_area_subscriptions");

            migrationBuilder.DropTable(
                name: "area_hotspots");

            migrationBuilder.DropTable(
                name: "feedback_provider_reports");

            migrationBuilder.DropTable(
                name: "provider_contracts");

            migrationBuilder.DropTable(
                name: "operating_areas");

            migrationBuilder.DropTable(
                name: "service_provider_coordinators");

            migrationBuilder.DropIndex(
                name: "IX_notifications_alert_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_feedbacks_area_id",
                table: "feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_feedback_resolutions_provider_report_id",
                table: "feedback_resolutions");

            migrationBuilder.DropIndex(
                name: "IX_analysis_results_detected_area_id",
                table: "analysis_results");

            migrationBuilder.DropIndex(
                name: "IX_ai_knowledge_sources_area_id",
                table: "ai_knowledge_sources");

            migrationBuilder.DropColumn(
                name: "alert_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "area_id",
                table: "feedbacks");

            migrationBuilder.DropColumn(
                name: "geo_source",
                table: "feedbacks");

            migrationBuilder.DropColumn(
                name: "is_location_verified",
                table: "feedbacks");

            migrationBuilder.DropColumn(
                name: "location_accuracy_meters",
                table: "feedbacks");

            migrationBuilder.DropColumn(
                name: "provider_report_id",
                table: "feedback_resolutions");

            migrationBuilder.DropColumn(
                name: "detected_area_id",
                table: "analysis_results");

            migrationBuilder.DropColumn(
                name: "area_id",
                table: "ai_knowledge_sources");

            migrationBuilder.RenameColumn(
                name: "created_by_staff_user_id",
                table: "feedback_resolutions",
                newName: "resolved_by_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_feedback_resolutions_created_by_staff_user_id",
                table: "feedback_resolutions",
                newName: "IX_feedback_resolutions_resolved_by_user_id");

            migrationBuilder.AddColumn<int>(
                name: "operator_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "operator_id",
                table: "feedback_resolutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "raw_response",
                table: "analysis_results",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    assigned_service_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValueSql: "'VND'::character varying"),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    schedule_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    service_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                name: "feedback_resolution_attachments",
                columns: table => new
                {
                    resolution_attachment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    resolution_id = table.Column<int>(type: "integer", nullable: false),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("feedback_resolution_attachments_pkey", x => x.resolution_attachment_id);
                    table.ForeignKey(
                        name: "fk_resolution_attachment_resolution",
                        column: x => x.resolution_id,
                        principalTable: "feedback_resolutions",
                        principalColumn: "resolution_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_operators",
                columns: table => new
                {
                    operator_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    operator_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_operators_pkey", x => x.operator_id);
                    table.ForeignKey(
                        name: "fk_service_operator_category",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_assignment_histories",
                columns: table => new
                {
                    history_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_service_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_service_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "service_executions",
                columns: table => new
                {
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    executed_by_service_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_taken = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    execution_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    result_note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                name: "service_payments",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValueSql: "'VND'::character varying"),
                    order_code = table.Column<long>(type: "bigint", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_link_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Pending'::character varying"),
                    transaction_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_payments_pkey", x => x.payment_id);
                    table.ForeignKey(
                        name: "fk_service_payment_booking",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_payment_user",
                        column: x => x.user_id,
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
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    is_satisfied = table.Column<bool>(type: "boolean", nullable: true),
                    rating = table.Column<int>(type: "integer", nullable: false)
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
                name: "feedback_assignments",
                columns: table => new
                {
                    assignment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_id = table.Column<int>(type: "integer", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    assignment_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Assigned'::character varying"),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("feedback_assignments_pkey", x => x.assignment_id);
                    table.ForeignKey(
                        name: "fk_feedback_assignment_assigned_by",
                        column: x => x.assigned_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_feedback_assignment_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_feedback_assignment_operator",
                        column: x => x.operator_id,
                        principalTable: "service_operators",
                        principalColumn: "operator_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "services",
                columns: table => new
                {
                    service_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    operator_id = table.Column<int>(type: "integer", nullable: false),
                    base_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValueSql: "'VND'::character varying"),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    external_service_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_system_service = table.Column<bool>(type: "boolean", nullable: false),
                    service_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("services_pkey", x => x.service_id);
                    table.ForeignKey(
                        name: "fk_service_category",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_operator",
                        column: x => x.operator_id,
                        principalTable: "service_operators",
                        principalColumn: "operator_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_execution_attachments",
                columns: table => new
                {
                    service_execution_attachment_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "booking_details",
                columns: table => new
                {
                    booking_detail_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_users_operator_id",
                table: "users",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_resolutions_operator_id",
                table: "feedback_resolutions",
                column: "operator_id");

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
                name: "IX_feedback_assignments_assigned_by_user_id",
                table: "feedback_assignments",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_assignments_feedback_id",
                table: "feedback_assignments",
                column: "feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_assignments_operator_id",
                table: "feedback_assignments",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_resolution_attachments_resolution_id",
                table: "feedback_resolution_attachments",
                column: "resolution_id");

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
                name: "IX_service_operators_category_id",
                table: "service_operators",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_payments_booking_id",
                table: "service_payments",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_payments_user_id",
                table: "service_payments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_service_payment_transaction_reference",
                table: "service_payments",
                column: "transaction_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_reviews_booking_id",
                table: "service_reviews",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_reviews_user_id",
                table: "service_reviews",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_services_category_id",
                table: "services",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "uq_service_operator_name",
                table: "services",
                columns: new[] { "operator_id", "service_name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_resolution_operator",
                table: "feedback_resolutions",
                column: "operator_id",
                principalTable: "service_operators",
                principalColumn: "operator_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_status_history_feedback",
                table: "feedback_status_histories",
                column: "feedback_id",
                principalTable: "feedbacks",
                principalColumn: "feedback_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_status_history_user",
                table: "feedback_status_histories",
                column: "changed_by_user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_message_attachment_interaction_message",
                table: "message_attachments",
                column: "interaction_message_id",
                principalTable: "interaction_messages",
                principalColumn: "interaction_message_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_operator",
                table: "users",
                column: "operator_id",
                principalTable: "service_operators",
                principalColumn: "operator_id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
