# Done — Incident API P0

Ngày hoàn tất: 2026-08-23.

## Đã làm

- Luồng tạo `Feedback` (Report) tạo đồng thời Incident, link `Primary`, subscription và
  audit event trong cùng transaction. Web và Messenger cùng đi qua service này.
- Confirm duplicate giữ contract cũ, đồng thời chuyển Report sang Incident canonical,
  soft-unlink lịch sử cũ và merge Incident rỗng sau khi relink.
- Thêm API staff:
  - `GET /api/management/incidents`
  - `GET /api/management/incidents/{incidentId}`
  - `POST /api/management/incidents/{incidentId}/reports`
  - `DELETE /api/management/incidents/{incidentId}/reports/{feedbackId}`
- Link/unlink dùng transaction, PostgreSQL advisory lock, kiểm tra active link duy nhất,
  idempotency và ghi audit event.
- DTO Feedback được bổ sung additive `incidentId`, `incidentReportCount` và
  `incidentLinkStatus`.
- Zalo bị tắt mặc định bằng `Zalo:Enabled=false`; webhook trả `404` trước khi ghi inbox
  hoặc queue và Zalo worker không được đăng ký. Messenger vẫn hoạt động.

## Đã kiểm chứng

- Build solution: 0 warning, 0 error.
- Unit test: 101/101 pass, gồm create/link/unlink/relink Incident và Zalo feature flag.
- Smoke read trên database đã migrate: Incident list/detail trả `200`, tổng 31 Incident
  sau backfill tại thời điểm kiểm tra.
- Smoke kênh: Zalo webhook trả `404`; Messenger verification trả `200` và challenge đúng.

## Chưa thuộc P0

- User Incident feed/detail/follow, candidate suggestion và merge/split API.
- Chuyển status, SLA, assignment, provider resolution và approval sang Incident.
- Các phần này tiếp tục nằm trong [`../incident-api-plan.md`](../incident-api-plan.md).
