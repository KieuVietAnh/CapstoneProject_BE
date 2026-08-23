# Done — Incident Match Workflow

Ngày hoàn tất: 2026-08-23.

## Hành vi

- `FeedbackDuplicateCandidate` tiếp tục được giữ để tương thích dữ liệu và client cũ,
  nhưng được diễn giải là đề xuất hai Report cùng thuộc một Incident.
- Confirm không xóa Feedback: Report được relink vào Incident canonical với role
  `Corroborating`; link cũ được soft-unlink và Incident rỗng được đánh dấu `Merged`.
- Reject không relink Report và không merge; hai Report tiếp tục thuộc hai Incident riêng.
- Confirm/Reject có tính idempotent khi retry cùng một quyết định; không relink hoặc gửi
  notification lặp.
- Notification cho người dân dùng ngôn ngữ “thông tin bổ sung cho cùng sự vụ” và xác nhận
  nội dung Report vẫn được lưu giữ.

## API

Route cũ vẫn hoạt động:

- `/api/staff/feedback-duplicates/*`

Route Incident-oriented mới là alias additive cho cùng workflow:

- `GET /api/management/incident-match-candidates/summary`
- `GET /api/management/incident-match-candidates`
- `GET /api/management/incident-match-candidates/{candidateId}`
- `POST /api/management/incident-match-candidates/{candidateId}/confirm`
- `POST /api/management/incident-match-candidates/{candidateId}/reject`

DTO candidate trả thêm `currentIncidentId`, `suggestedIncidentId` và
`areInSameIncident`; field `incidentId` cũ được giữ.

## Kiểm chứng

- 105/105 unit test pass.
- Test bao phủ Incident IDs trong candidate response, route alias, notification mới,
  confirm/reject idempotency và relink/merge Incident hiện hữu.
- Không có schema change hoặc migration mới cho lát cắt này.
