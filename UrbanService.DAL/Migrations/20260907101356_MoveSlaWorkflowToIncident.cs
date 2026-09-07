using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <summary>
    /// Chuyển quyền sở hữu SLA từ Report (Feedback) sang Incident và đổi tên
    /// aggregate thành IncidentSla.
    ///
    /// Migration này được viết tay thay cho bản scaffold, vì EF sinh ra
    /// DropTable + CreateTable (mất toàn bộ SLA đang chạy) và RenameColumn cho
    /// khóa ngoại (giữ nguyên UUID của Feedback trong cột incident_id, sai dữ
    /// liệu một cách âm thầm). Ở đây bảng được đổi tên tại chỗ để giữ nguyên
    /// dữ liệu, còn khóa ngoại được ánh xạ qua incident_report_links.
    /// </summary>
    public partial class MoveSlaWorkflowToIncident : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Đổi tên bảng và toàn bộ đối tượng schema đi kèm, giữ nguyên dữ liệu.
            migrationBuilder.Sql(
                """
                ALTER TABLE feedback_slas RENAME TO incident_slas;
                ALTER SEQUENCE IF EXISTS feedback_slas_feedback_sla_id_seq
                    RENAME TO incident_slas_incident_sla_id_seq;

                ALTER TABLE incident_slas RENAME COLUMN feedback_sla_id TO incident_sla_id;
                ALTER TABLE incident_slas RENAME CONSTRAINT feedback_slas_pkey TO incident_slas_pkey;
                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_feedback_slas_categories TO fk_incident_slas_categories;
                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_feedback_slas_completed_by_user TO fk_incident_slas_completed_by_user;
                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_feedback_slas_operating_areas TO fk_incident_slas_operating_areas;
                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_feedback_slas_sla_policies TO fk_incident_slas_sla_policies;
                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_feedback_slas_started_by_user TO fk_incident_slas_started_by_user;

                ALTER INDEX "IX_feedback_slas_area_id" RENAME TO "IX_incident_slas_area_id";
                ALTER INDEX "IX_feedback_slas_category_id" RENAME TO "IX_incident_slas_category_id";
                ALTER INDEX "IX_feedback_slas_completed_by_user_id" RENAME TO "IX_incident_slas_completed_by_user_id";
                ALTER INDEX "IX_feedback_slas_sla_policy_id" RENAME TO "IX_incident_slas_sla_policy_id";
                ALTER INDEX "IX_feedback_slas_started_by_user_id" RENAME TO "IX_incident_slas_started_by_user_id";
                ALTER INDEX ix_feedback_slas_monitoring RENAME TO ix_incident_slas_monitoring;

                ALTER TABLE sla_events RENAME COLUMN feedback_sla_id TO incident_sla_id;
                ALTER TABLE sla_events
                    RENAME CONSTRAINT fk_sla_events_feedback_slas TO fk_sla_events_incident_slas;
                ALTER INDEX ix_sla_events_feedback_sla_created_at
                    RENAME TO ix_sla_events_incident_sla_created_at;

                ALTER TABLE sla_pause_histories RENAME COLUMN feedback_sla_id TO incident_sla_id;
                ALTER TABLE sla_pause_histories
                    RENAME CONSTRAINT fk_sla_pause_histories_feedback_slas TO fk_sla_pause_histories_incident_slas;
                ALTER INDEX ix_sla_pause_histories_feedback_sla_id
                    RENAME TO ix_sla_pause_histories_incident_sla_id;
                """);

            // 2. Gỡ ràng buộc cũ về Feedback trước khi đổi chủ sở hữu.
            migrationBuilder.Sql(
                """
                ALTER TABLE incident_slas
                    DROP CONSTRAINT IF EXISTS fk_feedback_slas_feedbacks;

                DROP INDEX IF EXISTS ux_feedback_slas_current_feedback;
                DROP INDEX IF EXISTS ix_feedback_slas_feedback_id;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "incident_id",
                table: "incident_slas",
                type: "uuid",
                nullable: true);

            // 3. Ánh xạ từng SLA sang Incident đang hoạt động của Report.
            migrationBuilder.Sql(
                """
                UPDATE incident_slas AS sla
                SET incident_id = link.incident_id
                FROM incident_report_links AS link
                WHERE link.feedback_id = sla.feedback_id
                  AND link.link_status = 'Active';

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM incident_slas WHERE incident_id IS NULL) THEN
                        RAISE EXCEPTION
                            'Cannot migrate SLA workflow: % SLA row(s) belong to a Report with no active Incident link. Inspect with: SELECT incident_sla_id, feedback_id FROM incident_slas WHERE feedback_id NOT IN (SELECT feedback_id FROM incident_report_links WHERE link_status = ''Active'');',
                            (SELECT COUNT(*) FROM incident_slas WHERE incident_id IS NULL);
                    END IF;
                END $$;
                """);

            /*
             * 4. Trước cutover, mỗi Report được xác minh đều có SLA riêng, nên một
             * Incident gộp nhiều Report có thể đang có nhiều SLA is_current. Chỉ một
             * SLA được giữ làm SLA hiện tại của Incident; các SLA còn lại được hạ cấp
             * thành lịch sử. Không xóa dòng nào để giữ nguyên timeline và audit cũ.
             *
             * Thứ tự ưu tiên giữ lại: Report đóng vai trò Primary, rồi tới SLA bắt đầu
             * sớm nhất, cuối cùng dùng incident_sla_id làm tie-breaker ổn định.
             */
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        sla.incident_sla_id,
                        ROW_NUMBER() OVER (
                            PARTITION BY sla.incident_id
                            ORDER BY
                                CASE WHEN link.link_role = 'Primary' THEN 0 ELSE 1 END,
                                sla.started_at,
                                sla.incident_sla_id
                        ) AS position
                    FROM incident_slas AS sla
                    LEFT JOIN incident_report_links AS link
                        ON link.feedback_id = sla.feedback_id
                       AND link.link_status = 'Active'
                    WHERE sla.is_current
                )
                UPDATE incident_slas AS sla
                SET is_current = FALSE,
                    updated_at = NOW() AT TIME ZONE 'UTC'
                FROM ranked
                WHERE ranked.incident_sla_id = sla.incident_sla_id
                  AND ranked.position > 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "incident_id",
                table: "incident_slas",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "feedback_id",
                table: "incident_slas");

            migrationBuilder.CreateIndex(
                name: "ux_incident_slas_current_incident",
                table: "incident_slas",
                column: "incident_id",
                unique: true,
                filter: "is_current = true");

            migrationBuilder.AddForeignKey(
                name: "fk_incident_slas_incidents",
                table: "incident_slas",
                column: "incident_id",
                principalTable: "incidents",
                principalColumn: "incident_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE incident_slas
                    DROP CONSTRAINT IF EXISTS fk_incident_slas_incidents;

                DROP INDEX IF EXISTS ux_incident_slas_current_incident;
                DROP INDEX IF EXISTS ix_incident_slas_incident_id;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "feedback_id",
                table: "incident_slas",
                type: "uuid",
                nullable: true);

            /*
             * Trả SLA về Report chính của Incident. Nếu Incident không còn Report nào
             * đang liên kết thì không thể khôi phục chủ sở hữu cũ.
             */
            migrationBuilder.Sql(
                """
                WITH primary_link AS (
                    SELECT DISTINCT ON (link.incident_id)
                        link.incident_id,
                        link.feedback_id
                    FROM incident_report_links AS link
                    JOIN feedbacks AS report
                        ON report.feedback_id = link.feedback_id
                    WHERE link.link_status = 'Active'
                    ORDER BY
                        link.incident_id,
                        CASE WHEN link.link_role = 'Primary' THEN 0 ELSE 1 END,
                        report.created_at,
                        link.feedback_id
                )
                UPDATE incident_slas AS sla
                SET feedback_id = primary_link.feedback_id
                FROM primary_link
                WHERE primary_link.incident_id = sla.incident_id;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM incident_slas WHERE feedback_id IS NULL) THEN
                        RAISE EXCEPTION
                            'Cannot roll back SLA workflow: % SLA row(s) belong to an Incident with no active Report link.',
                            (SELECT COUNT(*) FROM incident_slas WHERE feedback_id IS NULL);
                    END IF;
                END $$;

                WITH ranked AS (
                    SELECT
                        incident_sla_id,
                        ROW_NUMBER() OVER (
                            PARTITION BY feedback_id
                            ORDER BY started_at, incident_sla_id
                        ) AS position
                    FROM incident_slas
                    WHERE is_current
                )
                UPDATE incident_slas AS sla
                SET is_current = FALSE
                FROM ranked
                WHERE ranked.incident_sla_id = sla.incident_sla_id
                  AND ranked.position > 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "feedback_id",
                table: "incident_slas",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "incident_id",
                table: "incident_slas");

            // Đổi tên ngược toàn bộ đối tượng schema về feedback_slas.
            migrationBuilder.Sql(
                """
                ALTER TABLE sla_pause_histories
                    RENAME CONSTRAINT fk_sla_pause_histories_incident_slas TO fk_sla_pause_histories_feedback_slas;
                ALTER INDEX ix_sla_pause_histories_incident_sla_id
                    RENAME TO ix_sla_pause_histories_feedback_sla_id;
                ALTER TABLE sla_pause_histories RENAME COLUMN incident_sla_id TO feedback_sla_id;

                ALTER TABLE sla_events
                    RENAME CONSTRAINT fk_sla_events_incident_slas TO fk_sla_events_feedback_slas;
                ALTER INDEX ix_sla_events_incident_sla_created_at
                    RENAME TO ix_sla_events_feedback_sla_created_at;
                ALTER TABLE sla_events RENAME COLUMN incident_sla_id TO feedback_sla_id;

                ALTER INDEX "IX_incident_slas_area_id" RENAME TO "IX_feedback_slas_area_id";
                ALTER INDEX "IX_incident_slas_category_id" RENAME TO "IX_feedback_slas_category_id";
                ALTER INDEX "IX_incident_slas_completed_by_user_id" RENAME TO "IX_feedback_slas_completed_by_user_id";
                ALTER INDEX "IX_incident_slas_sla_policy_id" RENAME TO "IX_feedback_slas_sla_policy_id";
                ALTER INDEX "IX_incident_slas_started_by_user_id" RENAME TO "IX_feedback_slas_started_by_user_id";
                ALTER INDEX ix_incident_slas_monitoring RENAME TO ix_feedback_slas_monitoring;

                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_incident_slas_categories TO fk_feedback_slas_categories;
                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_incident_slas_completed_by_user TO fk_feedback_slas_completed_by_user;
                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_incident_slas_operating_areas TO fk_feedback_slas_operating_areas;
                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_incident_slas_sla_policies TO fk_feedback_slas_sla_policies;
                ALTER TABLE incident_slas
                    RENAME CONSTRAINT fk_incident_slas_started_by_user TO fk_feedback_slas_started_by_user;
                ALTER TABLE incident_slas RENAME CONSTRAINT incident_slas_pkey TO feedback_slas_pkey;
                ALTER TABLE incident_slas RENAME COLUMN incident_sla_id TO feedback_sla_id;

                ALTER SEQUENCE IF EXISTS incident_slas_incident_sla_id_seq
                    RENAME TO feedback_slas_feedback_sla_id_seq;
                ALTER TABLE incident_slas RENAME TO feedback_slas;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_feedback_slas_current_feedback",
                table: "feedback_slas",
                column: "feedback_id",
                unique: true,
                filter: "is_current = true");

            migrationBuilder.AddForeignKey(
                name: "fk_feedback_slas_feedbacks",
                table: "feedback_slas",
                column: "feedback_id",
                principalTable: "feedbacks",
                principalColumn: "feedback_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
