using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentAggregateSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    area_id = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    location_text = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValueSql: "'Medium'::character varying"),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'New'::character varying"),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    merged_into_incident_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("incidents_pkey", x => x.incident_id);
                    table.CheckConstraint("ck_incident_merge_not_self", "merged_into_incident_id IS NULL OR merged_into_incident_id <> incident_id");
                    table.CheckConstraint("ck_incident_status", "status IN ('New', 'Submitted', 'AiReviewed', 'Verified', 'Assigned', 'InProgress', 'Resolved', 'SubmittedForApproval', 'Approved', 'Rejected', 'NeedRework', 'Closed', 'Cancelled', 'Merged')");
                    table.ForeignKey(
                        name: "fk_incident_area",
                        column: x => x.area_id,
                        principalTable: "operating_areas",
                        principalColumn: "area_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incident_category",
                        column: x => x.category_id,
                        principalTable: "urban_service_categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incident_merged_into",
                        column: x => x.merged_into_incident_id,
                        principalTable: "incidents",
                        principalColumn: "incident_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incident_events",
                columns: table => new
                {
                    incident_event_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("incident_events_pkey", x => x.incident_event_id);
                    table.ForeignKey(
                        name: "fk_incident_event_actor",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_incident_event_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_incident_event_incident",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "incident_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incident_report_links",
                columns: table => new
                {
                    incident_report_link_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    link_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    link_role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Corroborating"),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    linked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    unlinked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unlinked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("incident_report_links_pkey", x => x.incident_report_link_id);
                    table.CheckConstraint("ck_incident_report_link_method", "link_method IN ('Created', 'Backfill', 'UserSelected', 'AiSuggested', 'StaffConfirmed')");
                    table.CheckConstraint("ck_incident_report_link_role", "link_role IN ('Primary', 'Corroborating')");
                    table.CheckConstraint("ck_incident_report_link_status", "link_status IN ('Active', 'Unlinked')");
                    table.CheckConstraint("ck_incident_report_link_unlinked", "(link_status = 'Active' AND unlinked_at IS NULL) OR (link_status = 'Unlinked' AND unlinked_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_incident_report_link_feedback",
                        column: x => x.feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_incident_report_link_incident",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "incident_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_incident_report_link_linked_by",
                        column: x => x.linked_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_incident_report_link_unlinked_by",
                        column: x => x.unlinked_by_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "incident_subscriptions",
                columns: table => new
                {
                    incident_subscription_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_feedback_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("incident_subscriptions_pkey", x => x.incident_subscription_id);
                    table.CheckConstraint("ck_incident_subscription_source_type", "source_type IN ('Report', 'Follow', 'Support', 'Backfill')");
                    table.ForeignKey(
                        name: "fk_incident_subscription_incident",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "incident_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_incident_subscription_source_feedback",
                        column: x => x.source_feedback_id,
                        principalTable: "feedbacks",
                        principalColumn: "feedback_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_incident_subscription_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incident_events_actor_user_id",
                table: "incident_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_events_feedback_id",
                table: "incident_events",
                column: "feedback_id");

            migrationBuilder.CreateIndex(
                name: "ix_incident_events_incident_created_at",
                table: "incident_events",
                columns: new[] { "incident_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_incident_report_links_incident_id",
                table: "incident_report_links",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_report_links_linked_by_user_id",
                table: "incident_report_links",
                column: "linked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_report_links_unlinked_by_user_id",
                table: "incident_report_links",
                column: "unlinked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_incident_report_links_active_feedback",
                table: "incident_report_links",
                column: "feedback_id",
                unique: true,
                filter: "link_status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_incident_subscriptions_source_feedback_id",
                table: "incident_subscriptions",
                column: "source_feedback_id");

            migrationBuilder.CreateIndex(
                name: "ix_incident_subscriptions_user_active",
                table: "incident_subscriptions",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_incident_subscriptions_incident_user",
                table: "incident_subscriptions",
                columns: new[] { "incident_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_incidents_area_status_created_at",
                table: "incidents",
                columns: new[] { "area_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_incidents_category_status",
                table: "incidents",
                columns: new[] { "category_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_incidents_merged_into_incident_id",
                table: "incidents",
                column: "merged_into_incident_id");

            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE incident_backfill_clusters
                ON COMMIT DROP
                AS
                SELECT
                    feedback_id AS root_feedback_id,
                    gen_random_uuid() AS incident_id
                FROM feedbacks
                WHERE parent_ticket_id IS NULL;

                INSERT INTO incidents (
                    incident_id,
                    area_id,
                    category_id,
                    title,
                    description,
                    location_text,
                    latitude,
                    longitude,
                    priority,
                    status,
                    due_date,
                    resolved_at,
                    closed_at,
                    created_at,
                    updated_at)
                SELECT
                    cluster.incident_id,
                    root.area_id,
                    root.category_id,
                    root.title,
                    root.description,
                    root.location_text,
                    root.latitude,
                    root.longitude,
                    root.priority,
                    CASE
                        WHEN root.status IN (
                            'Submitted',
                            'AiReviewed',
                            'Verified',
                            'Assigned',
                            'InProgress',
                            'Resolved',
                            'SubmittedForApproval',
                            'Approved',
                            'Rejected',
                            'NeedRework',
                            'Closed',
                            'Cancelled')
                        THEN root.status
                        ELSE 'New'
                    END,
                    root.due_date,
                    CASE
                        WHEN root.status IN ('Resolved', 'SubmittedForApproval', 'Approved', 'Closed')
                        THEN COALESCE(root.updated_at, root.created_at)
                        ELSE NULL
                    END,
                    CASE
                        WHEN root.status = 'Closed'
                        THEN COALESCE(root.updated_at, root.created_at)
                        ELSE NULL
                    END,
                    root.created_at,
                    root.updated_at
                FROM incident_backfill_clusters AS cluster
                INNER JOIN feedbacks AS root
                    ON root.feedback_id = cluster.root_feedback_id;

                INSERT INTO incident_report_links (
                    incident_report_link_id,
                    incident_id,
                    feedback_id,
                    link_status,
                    link_method,
                    link_role,
                    reason,
                    linked_at)
                SELECT
                    gen_random_uuid(),
                    cluster.incident_id,
                    report.feedback_id,
                    'Active',
                    'Backfill',
                    CASE
                        WHEN report.feedback_id = cluster.root_feedback_id THEN 'Primary'
                        ELSE 'Corroborating'
                    END,
                    'Created by AddIncidentAggregateSchema migration from the legacy feedback cluster.',
                    now()
                FROM feedbacks AS report
                INNER JOIN incident_backfill_clusters AS cluster
                    ON cluster.root_feedback_id = COALESCE(report.parent_ticket_id, report.feedback_id);

                INSERT INTO incident_subscriptions (
                    incident_subscription_id,
                    incident_id,
                    user_id,
                    source_type,
                    source_feedback_id,
                    is_active,
                    created_at)
                SELECT
                    gen_random_uuid(),
                    subscription.incident_id,
                    subscription.user_id,
                    'Backfill',
                    subscription.feedback_id,
                    TRUE,
                    now()
                FROM (
                    SELECT DISTINCT ON (cluster.incident_id, report.user_id)
                        cluster.incident_id,
                        report.user_id,
                        report.feedback_id
                    FROM feedbacks AS report
                    INNER JOIN incident_backfill_clusters AS cluster
                        ON cluster.root_feedback_id = COALESCE(report.parent_ticket_id, report.feedback_id)
                    ORDER BY cluster.incident_id, report.user_id, report.created_at, report.feedback_id
                ) AS subscription;

                INSERT INTO incident_events (
                    incident_id,
                    event_type,
                    payload_json,
                    created_at)
                SELECT
                    cluster.incident_id,
                    'IncidentBackfilled',
                    jsonb_build_object('rootFeedbackId', cluster.root_feedback_id),
                    now()
                FROM incident_backfill_clusters AS cluster;

                INSERT INTO incident_events (
                    incident_id,
                    feedback_id,
                    event_type,
                    payload_json,
                    created_at)
                SELECT
                    link.incident_id,
                    link.feedback_id,
                    'ReportLinkedBackfill',
                    jsonb_build_object('linkMethod', 'Backfill'),
                    link.linked_at
                FROM incident_report_links AS link
                WHERE link.link_method = 'Backfill';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_events");

            migrationBuilder.DropTable(
                name: "incident_report_links");

            migrationBuilder.DropTable(
                name: "incident_subscriptions");

            migrationBuilder.DropTable(
                name: "incidents");
        }
    }
}
