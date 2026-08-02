using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <inheritdoc />
    public partial class EnforceFeedbackDuplicateMasterState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Normalize legacy status casing so the filtered unique index cannot
                -- be bypassed by values such as "pending" or "CONFIRMED".
                UPDATE feedback_duplicate_candidates
                SET status = CASE lower(status)
                        WHEN 'pending' THEN 'Pending'
                        WHEN 'confirmed' THEN 'Confirmed'
                        WHEN 'rejected' THEN 'Rejected'
                        ELSE 'Rejected'
                    END,
                    updated_at = now(),
                    reason = CASE
                        WHEN lower(status) IN ('pending', 'confirmed', 'rejected') THEN reason
                        ELSE concat_ws(
                            E'\n',
                            NULLIF(reason, ''),
                            '[Migration] Trạng thái candidate không hợp lệ đã được chuyển thành Rejected.')
                    END
                WHERE status NOT IN ('Pending', 'Confirmed', 'Rejected');

                -- Confirmed duplicates whose master cannot be shown to residents must
                -- return to Pending. Their parent link will only be created again after
                -- the master reaches a public, valid status.
                UPDATE feedback_duplicate_candidates AS candidate
                SET status = 'Pending',
                    reviewed_by_user_id = NULL,
                    reviewed_at = NULL,
                    updated_at = now(),
                    reason = concat_ws(
                        E'\n',
                        NULLIF(candidate.reason, ''),
                        '[Migration] Chờ phản ánh chính được công khai trước khi xác nhận trùng.')
                FROM feedbacks AS parent
                WHERE candidate.status = 'Confirmed'
                  AND parent.feedback_id = candidate.potential_parent_feedback_id
                  AND parent.status NOT IN (
                      'Verified',
                      'Assigned',
                      'InProgress',
                      'Resolved',
                      'SubmittedForApproval',
                      'Approved',
                      'NeedRework',
                      'Closed');

                UPDATE feedbacks AS child
                SET parent_ticket_id = NULL,
                    is_master_ticket = FALSE,
                    updated_at = now()
                FROM feedback_duplicate_candidates AS candidate
                WHERE candidate.feedback_id = child.feedback_id
                  AND candidate.status = 'Pending'
                  AND child.parent_ticket_id = candidate.potential_parent_feedback_id;

                -- Remove impossible self-relations before adding database checks.
                UPDATE feedbacks
                SET parent_ticket_id = NULL,
                    is_master_ticket = FALSE,
                    updated_at = now()
                WHERE parent_ticket_id = feedback_id;

                DELETE FROM feedback_duplicate_candidates
                WHERE feedback_id = potential_parent_feedback_id;

                -- Keep a single actionable candidate per feedback. Prefer an already
                -- Confirmed relation, otherwise keep the oldest Pending suggestion.
                WITH ranked_active_candidates AS (
                    SELECT candidate.duplicate_candidate_id,
                           row_number() OVER (
                               PARTITION BY candidate.feedback_id
                               ORDER BY
                                   CASE
                                       WHEN candidate.status = 'Confirmed'
                                        AND child.parent_ticket_id = candidate.potential_parent_feedback_id THEN 0
                                       WHEN candidate.status = 'Confirmed' THEN 1
                                       ELSE 2
                                   END,
                                   candidate.reviewed_at DESC NULLS LAST,
                                   candidate.created_at,
                                   candidate.duplicate_candidate_id) AS candidate_rank
                    FROM feedback_duplicate_candidates AS candidate
                    JOIN feedbacks AS child
                      ON child.feedback_id = candidate.feedback_id
                    WHERE candidate.status IN ('Pending', 'Confirmed')
                )
                UPDATE feedback_duplicate_candidates AS candidate
                SET status = 'Rejected',
                    reviewed_by_user_id = NULL,
                    reviewed_at = now(),
                    updated_at = now(),
                    reason = concat_ws(
                        E'\n',
                        NULLIF(candidate.reason, ''),
                        '[Migration] Từ chối candidate cạnh tranh; hệ thống chỉ giữ một liên kết đang hoạt động.')
                FROM ranked_active_candidates AS ranked
                WHERE ranked.duplicate_candidate_id = candidate.duplicate_candidate_id
                  AND ranked.candidate_rank > 1;

                -- Break legacy ParentTicketId cycles before flattening. A cycle has no
                -- canonical root, so every member is detached and reclassified below.
                WITH RECURSIVE parent_walk AS (
                    SELECT feedback_id AS origin_id,
                           parent_ticket_id AS current_id,
                           ARRAY[feedback_id]::uuid[] AS visited
                    FROM feedbacks
                    WHERE parent_ticket_id IS NOT NULL

                    UNION ALL

                    SELECT walk.origin_id,
                           parent.parent_ticket_id,
                           walk.visited || walk.current_id
                    FROM parent_walk AS walk
                    JOIN feedbacks AS parent
                      ON parent.feedback_id = walk.current_id
                    WHERE walk.current_id IS NOT NULL
                      AND NOT walk.current_id = ANY(walk.visited)
                ),
                cyclic_feedbacks AS (
                    SELECT DISTINCT origin_id AS feedback_id
                    FROM parent_walk
                    WHERE current_id = ANY(visited)
                )
                UPDATE feedbacks AS feedback
                SET parent_ticket_id = NULL,
                    is_master_ticket = FALSE,
                    updated_at = now()
                FROM cyclic_feedbacks AS cyclic
                WHERE cyclic.feedback_id = feedback.feedback_id;

                -- Flatten confirmed ParentTicketId chains so every duplicate points
                -- directly to the terminal root instead of producing A <- B <- C.
                WITH RECURSIVE parent_paths AS (
                    SELECT feedback_id AS child_id,
                           parent_ticket_id AS current_parent_id,
                           ARRAY[feedback_id]::uuid[] AS visited
                    FROM feedbacks
                    WHERE parent_ticket_id IS NOT NULL

                    UNION ALL

                    SELECT path.child_id,
                           parent.parent_ticket_id,
                           path.visited || path.current_parent_id
                    FROM parent_paths AS path
                    JOIN feedbacks AS parent
                      ON parent.feedback_id = path.current_parent_id
                    WHERE parent.parent_ticket_id IS NOT NULL
                      AND NOT path.current_parent_id = ANY(path.visited)
                ),
                resolved_roots AS (
                    SELECT DISTINCT ON (path.child_id)
                           path.child_id,
                           path.current_parent_id AS root_id
                    FROM parent_paths AS path
                    JOIN feedbacks AS root
                      ON root.feedback_id = path.current_parent_id
                    WHERE root.parent_ticket_id IS NULL
                    ORDER BY path.child_id, cardinality(path.visited) DESC
                )
                UPDATE feedbacks AS child
                SET parent_ticket_id = root.root_id,
                    is_master_ticket = FALSE,
                    updated_at = now()
                FROM resolved_roots AS root
                WHERE child.feedback_id = root.child_id
                  AND child.parent_ticket_id IS DISTINCT FROM root.root_id;

                -- Remove legacy links that could never be confirmed under the new
                -- canonical-master rules, including orphan links without a candidate.
                UPDATE feedbacks AS child
                SET parent_ticket_id = NULL,
                    is_master_ticket = FALSE,
                    updated_at = now()
                FROM feedbacks AS parent
                WHERE child.parent_ticket_id = parent.feedback_id
                  AND (
                      parent.status NOT IN (
                          'Verified',
                          'Assigned',
                          'InProgress',
                          'Resolved',
                          'SubmittedForApproval',
                          'Approved',
                          'NeedRework',
                          'Closed')
                      OR parent.area_id <> child.area_id
                      OR parent.created_at > child.created_at
                      OR (
                          parent.created_at = child.created_at
                          AND parent.feedback_id::text >= child.feedback_id::text));

                -- Resolve Pending/Confirmed candidate chains to their canonical root.
                CREATE TEMP TABLE duplicate_candidate_roots ON COMMIT DROP AS
                WITH RECURSIVE active_edges AS (
                    SELECT feedback_id AS child_id,
                           potential_parent_feedback_id AS parent_id
                    FROM feedback_duplicate_candidates
                    WHERE status IN ('Pending', 'Confirmed')
                ),
                candidate_paths AS (
                    SELECT candidate.duplicate_candidate_id,
                           candidate.feedback_id AS child_id,
                           candidate.potential_parent_feedback_id AS current_id,
                           ARRAY[candidate.feedback_id]::uuid[] AS visited
                    FROM feedback_duplicate_candidates AS candidate
                    WHERE candidate.status IN ('Pending', 'Confirmed')

                    UNION ALL

                    SELECT path.duplicate_candidate_id,
                           path.child_id,
                           COALESCE(current.parent_ticket_id, edge.parent_id),
                           path.visited || path.current_id
                    FROM candidate_paths AS path
                    JOIN feedbacks AS current
                      ON current.feedback_id = path.current_id
                    LEFT JOIN active_edges AS edge
                      ON edge.child_id = path.current_id
                    WHERE COALESCE(current.parent_ticket_id, edge.parent_id) IS NOT NULL
                      AND NOT path.current_id = ANY(path.visited)
                )
                SELECT DISTINCT ON (path.duplicate_candidate_id)
                       path.duplicate_candidate_id,
                       path.child_id,
                       path.current_id AS root_id
                FROM candidate_paths AS path
                JOIN feedbacks AS root
                  ON root.feedback_id = path.current_id
                LEFT JOIN active_edges AS outgoing
                  ON outgoing.child_id = path.current_id
                WHERE root.parent_ticket_id IS NULL
                  AND outgoing.child_id IS NULL
                ORDER BY path.duplicate_candidate_id, cardinality(path.visited) DESC;

                -- Cyclic/unresolvable candidates cannot remain actionable.
                UPDATE feedback_duplicate_candidates AS candidate
                SET status = 'Rejected',
                    reviewed_by_user_id = NULL,
                    reviewed_at = now(),
                    updated_at = now(),
                    reason = concat_ws(
                        E'\n',
                        NULLIF(candidate.reason, ''),
                        '[Migration] Candidate không có phản ánh chính hợp lệ và cần được kiểm tra lại.')
                WHERE candidate.status IN ('Pending', 'Confirmed')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM duplicate_candidate_roots AS root
                      WHERE root.duplicate_candidate_id = candidate.duplicate_candidate_id
                        AND root.root_id <> candidate.feedback_id);

                -- If the canonical pair already exists historically, reuse that row and
                -- reject the chained source row before reactivating it to avoid conflicts.
                CREATE TEMP TABLE duplicate_candidate_conflicts ON COMMIT DROP AS
                SELECT root.duplicate_candidate_id AS source_id,
                       existing.duplicate_candidate_id AS target_id,
                       source.status AS source_status,
                       source.confidence_score,
                       source.reason,
                       source.reviewed_by_user_id,
                       source.reviewed_at,
                       root.root_id
                FROM duplicate_candidate_roots AS root
                JOIN feedback_duplicate_candidates AS source
                  ON source.duplicate_candidate_id = root.duplicate_candidate_id
                 AND source.status IN ('Pending', 'Confirmed')
                JOIN feedback_duplicate_candidates AS existing
                  ON existing.feedback_id = source.feedback_id
                 AND existing.potential_parent_feedback_id = root.root_id
                 AND existing.duplicate_candidate_id <> source.duplicate_candidate_id
                WHERE source.potential_parent_feedback_id <> root.root_id;

                UPDATE feedback_duplicate_candidates AS source
                SET status = 'Rejected',
                    reviewed_by_user_id = NULL,
                    reviewed_at = now(),
                    updated_at = now(),
                    reason = concat_ws(
                        E'\n',
                        NULLIF(source.reason, ''),
                        '[Migration] Candidate chuỗi đã được quy về phản ánh chính.')
                FROM duplicate_candidate_conflicts AS conflict
                WHERE conflict.source_id = source.duplicate_candidate_id;

                UPDATE feedback_duplicate_candidates AS target
                SET status = conflict.source_status,
                    confidence_score = conflict.confidence_score,
                    reason = conflict.reason,
                    reviewed_by_user_id = conflict.reviewed_by_user_id,
                    reviewed_at = conflict.reviewed_at,
                    updated_at = now()
                FROM duplicate_candidate_conflicts AS conflict
                WHERE conflict.target_id = target.duplicate_candidate_id;

                UPDATE feedback_duplicate_candidates AS candidate
                SET potential_parent_feedback_id = root.root_id,
                    updated_at = now()
                FROM duplicate_candidate_roots AS root
                WHERE root.duplicate_candidate_id = candidate.duplicate_candidate_id
                  AND candidate.status IN ('Pending', 'Confirmed')
                  AND candidate.potential_parent_feedback_id <> root.root_id
                  AND NOT EXISTS (
                      SELECT 1
                      FROM duplicate_candidate_conflicts AS conflict
                      WHERE conflict.source_id = candidate.duplicate_candidate_id);

                -- Canonicalization may change B -> A into a direct C -> A relation.
                -- Revalidate the final root before materializing ParentTicketId.
                UPDATE feedbacks AS child
                SET parent_ticket_id = NULL,
                    is_master_ticket = FALSE,
                    updated_at = now()
                FROM feedback_duplicate_candidates AS candidate
                JOIN feedbacks AS parent
                  ON parent.feedback_id = candidate.potential_parent_feedback_id
                WHERE candidate.feedback_id = child.feedback_id
                  AND candidate.status = 'Confirmed'
                  AND (
                      parent.status NOT IN (
                          'Verified',
                          'Assigned',
                          'InProgress',
                          'Resolved',
                          'SubmittedForApproval',
                          'Approved',
                          'NeedRework',
                          'Closed')
                      OR parent.area_id <> child.area_id
                      OR parent.created_at > child.created_at
                      OR (
                          parent.created_at = child.created_at
                          AND parent.feedback_id::text >= child.feedback_id::text));

                -- Internal roots can become public later, so retain the suggestion as
                -- Pending. Permanently invalid roots and structural mismatches are rejected.
                UPDATE feedback_duplicate_candidates AS candidate
                SET status = 'Pending',
                    reviewed_by_user_id = NULL,
                    reviewed_at = NULL,
                    updated_at = now(),
                    reason = concat_ws(
                        E'\n',
                        NULLIF(candidate.reason, ''),
                        '[Migration] Chờ phản ánh chính được công khai trước khi xác nhận trùng.')
                FROM feedbacks AS child,
                     feedbacks AS parent
                WHERE child.feedback_id = candidate.feedback_id
                  AND parent.feedback_id = candidate.potential_parent_feedback_id
                  AND candidate.status = 'Confirmed'
                  AND parent.status IN ('Submitted', 'AiReviewed')
                  AND parent.area_id = child.area_id
                  AND (
                      parent.created_at < child.created_at
                      OR (
                          parent.created_at = child.created_at
                          AND parent.feedback_id::text < child.feedback_id::text));

                UPDATE feedback_duplicate_candidates AS candidate
                SET status = 'Rejected',
                    reviewed_by_user_id = NULL,
                    reviewed_at = now(),
                    updated_at = now(),
                    reason = concat_ws(
                        E'\n',
                        NULLIF(candidate.reason, ''),
                        '[Migration] Root sau chuẩn hóa không đáp ứng điều kiện phản ánh chính.')
                FROM feedbacks AS child,
                     feedbacks AS parent
                WHERE child.feedback_id = candidate.feedback_id
                  AND parent.feedback_id = candidate.potential_parent_feedback_id
                  AND candidate.status IN ('Pending', 'Confirmed')
                  AND (
                      parent.status NOT IN (
                          'Submitted',
                          'AiReviewed',
                          'Verified',
                          'Assigned',
                          'InProgress',
                          'Resolved',
                          'SubmittedForApproval',
                          'Approved',
                          'NeedRework',
                          'Closed')
                      OR parent.area_id <> child.area_id
                      OR parent.created_at > child.created_at
                      OR (
                          parent.created_at = child.created_at
                          AND parent.feedback_id::text >= child.feedback_id::text));

                -- Confirmed rows own the concrete ParentTicketId. Pending rows remain
                -- unlinked and ineligible to become a parent until staff decides.
                UPDATE feedbacks AS child
                SET parent_ticket_id = candidate.potential_parent_feedback_id,
                    is_master_ticket = FALSE,
                    updated_at = now()
                FROM feedback_duplicate_candidates AS candidate
                JOIN feedbacks AS parent
                  ON parent.feedback_id = candidate.potential_parent_feedback_id
                WHERE candidate.feedback_id = child.feedback_id
                  AND candidate.status = 'Confirmed'
                  AND parent.status IN (
                      'Verified',
                      'Assigned',
                      'InProgress',
                      'Resolved',
                      'SubmittedForApproval',
                      'Approved',
                      'NeedRework',
                      'Closed')
                  AND parent.area_id = child.area_id
                  AND (
                      parent.created_at < child.created_at
                      OR (
                          parent.created_at = child.created_at
                          AND parent.feedback_id::text < child.feedback_id::text));

                UPDATE feedbacks
                SET is_master_ticket = FALSE;

                UPDATE feedbacks AS feedback
                SET is_master_ticket = TRUE
                WHERE feedback.parent_ticket_id IS NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM feedback_duplicate_candidates AS candidate
                      WHERE candidate.feedback_id = feedback.feedback_id
                        AND candidate.status IN ('Pending', 'Confirmed'));
                """);

            migrationBuilder.CreateIndex(
                name: "ix_feedbacks_duplicate_master_lookup",
                table: "feedbacks",
                columns: new[] { "area_id", "is_master_ticket", "created_at" },
                filter: "is_master_ticket = TRUE AND parent_ticket_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_feedback_master_has_no_parent",
                table: "feedbacks",
                sql: "NOT is_master_ticket OR parent_ticket_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_feedback_parent_not_self",
                table: "feedbacks",
                sql: "parent_ticket_id IS NULL OR parent_ticket_id <> feedback_id");

            migrationBuilder.CreateIndex(
                name: "uq_feedback_duplicate_candidate_active_child",
                table: "feedback_duplicate_candidates",
                column: "feedback_id",
                unique: true,
                filter: "status IN ('Pending', 'Confirmed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_feedback_duplicate_candidate_not_self",
                table: "feedback_duplicate_candidates",
                sql: "feedback_id <> potential_parent_feedback_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_feedback_duplicate_candidate_status",
                table: "feedback_duplicate_candidates",
                sql: "status IN ('Pending', 'Confirmed', 'Rejected')");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION enforce_feedback_duplicate_parent()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    parent_feedback feedbacks%ROWTYPE;
                BEGIN
                    IF NEW.parent_ticket_id IS NULL THEN
                        RETURN NEW;
                    END IF;

                    PERFORM pg_advisory_xact_lock(
                        hashtextextended(NEW.parent_ticket_id::text, 49101));

                    SELECT *
                    INTO parent_feedback
                    FROM feedbacks
                    WHERE feedback_id = NEW.parent_ticket_id;

                    IF NOT FOUND THEN
                        RETURN NEW;
                    END IF;

                    IF NEW.parent_ticket_id = NEW.feedback_id
                       OR NOT parent_feedback.is_master_ticket
                       OR parent_feedback.parent_ticket_id IS NOT NULL
                       OR parent_feedback.status NOT IN (
                           'Verified',
                           'Assigned',
                           'InProgress',
                           'Resolved',
                           'SubmittedForApproval',
                           'Approved',
                           'NeedRework',
                           'Closed')
                       OR parent_feedback.area_id <> NEW.area_id
                       OR parent_feedback.created_at > NEW.created_at
                       OR (
                           parent_feedback.created_at = NEW.created_at
                           AND parent_feedback.feedback_id::text >= NEW.feedback_id::text) THEN
                        RAISE EXCEPTION
                            'ParentTicketId must reference an older, public canonical master in the same area.';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE OR REPLACE FUNCTION enforce_feedback_master_with_children()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    PERFORM pg_advisory_xact_lock(
                        hashtextextended(NEW.feedback_id::text, 49101));

                    IF EXISTS (
                        SELECT 1
                        FROM feedbacks AS child
                        WHERE child.parent_ticket_id = NEW.feedback_id)
                       AND (
                           NOT NEW.is_master_ticket
                           OR NEW.parent_ticket_id IS NOT NULL
                           OR NEW.status NOT IN (
                               'Verified',
                               'Assigned',
                               'InProgress',
                               'Resolved',
                               'SubmittedForApproval',
                               'Approved',
                               'NeedRework',
                               'Closed')
                           OR EXISTS (
                               SELECT 1
                               FROM feedbacks AS child
                               WHERE child.parent_ticket_id = NEW.feedback_id
                                 AND (
                                     child.area_id <> NEW.area_id
                                     OR NEW.created_at > child.created_at
                                     OR (
                                         NEW.created_at = child.created_at
                                         AND NEW.feedback_id::text >= child.feedback_id::text)))) THEN
                        RAISE EXCEPTION
                            'A feedback with duplicate children must remain a public canonical master.';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_enforce_feedback_duplicate_parent
                BEFORE INSERT OR UPDATE OF parent_ticket_id, area_id, created_at
                ON feedbacks
                FOR EACH ROW
                EXECUTE FUNCTION enforce_feedback_duplicate_parent();

                CREATE TRIGGER trg_enforce_feedback_master_with_children
                BEFORE UPDATE OF status, is_master_ticket, parent_ticket_id, area_id, created_at
                ON feedbacks
                FOR EACH ROW
                EXECUTE FUNCTION enforce_feedback_master_with_children();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_enforce_feedback_master_with_children ON feedbacks;
                DROP TRIGGER IF EXISTS trg_enforce_feedback_duplicate_parent ON feedbacks;
                DROP FUNCTION IF EXISTS enforce_feedback_master_with_children();
                DROP FUNCTION IF EXISTS enforce_feedback_duplicate_parent();
                """);

            migrationBuilder.DropIndex(
                name: "ix_feedbacks_duplicate_master_lookup",
                table: "feedbacks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_feedback_master_has_no_parent",
                table: "feedbacks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_feedback_parent_not_self",
                table: "feedbacks");

            migrationBuilder.DropIndex(
                name: "uq_feedback_duplicate_candidate_active_child",
                table: "feedback_duplicate_candidates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_feedback_duplicate_candidate_not_self",
                table: "feedback_duplicate_candidates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_feedback_duplicate_candidate_status",
                table: "feedback_duplicate_candidates");

        }
    }
}
