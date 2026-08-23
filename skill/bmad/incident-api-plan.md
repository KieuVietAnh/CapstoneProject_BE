# Incident API Plan — BE

Mục tiêu: đưa schema Incident vào luồng chạy theo từng phase, giữ tương thích các API
Feedback hiện tại và chỉ chuyển workflow sau khi intake/linking ổn định.

Trạng thái 2026-08-23: **P0 đã hoàn tất** và được lưu tại
[`done/incident-api-p0.md`](done/incident-api-p0.md). P1/P2 còn mở. Theo quyết định hiện
tại, Zalo bị tắt; intake đang áp dụng cho Web và Messenger.

## P0 — đã hoàn tất

### 1. Sửa API tạo Report hiện hữu

- Giữ `POST /api/user/feedbacks` và response hiện tại.
- Khi lưu Feedback, gọi `IIncidentService.StageNewReportIncidentAsync` trong cùng transaction:
  - tạo Incident mới nếu chưa có lựa chọn/match đủ tin cậy;
  - hoặc tạo `IncidentReportLink` tới Incident đã chọn/xác nhận;
  - tạo `IncidentSubscription` cho người gửi;
  - ghi `IncidentEvent`.
- Bổ sung field không phá vỡ contract: `incidentId`, `incidentReportCount`, `incidentLinkStatus`.
- Messenger dùng chung `FeedbackService`, không nhân bản logic trong webhook. Zalo giữ
  code tương thích nhưng bị chặn bằng feature flag và không tiếp nhận Report.

Đây là API cần sửa đầu tiên vì nếu chỉ apply schema mà không dual-write thì Report mới sẽ
không có Incident.

### 2. Thêm API đọc Incident cho staff

- `GET /api/management/incidents`: queue có phân trang; filter theo area, category,
  status, priority, thời gian và số report.
- `GET /api/management/incidents/{incidentId}`: chi tiết canonical, tổng report,
  danh sách report/link, subscribers và event timeline.

Hai API này cho phép staff vận hành trên một sự vụ thay vì mở từng Feedback độc lập.

### 3. Thêm API link/unlink Report

- `POST /api/management/incidents/{incidentId}/reports`: link một Feedback vào Incident;
  body gồm `feedbackId`, `method`, `confidenceScore`, `reason`.
- `DELETE /api/management/incidents/{incidentId}/reports/{feedbackId}`: soft-unlink,
  cập nhật link thành `Unlinked` và ghi event; không xóa lịch sử.
- Cả hai phải transaction, idempotent và kiểm tra Report không có active link khác.

### 4. Sửa confirm duplicate hiện hữu

- Giữ tạm `POST /api/staff/feedback-duplicates/{duplicateCandidateId}/confirm`.
- Khi confirm, ngoài cập nhật legacy `ParentTicketId`, phải link Report vào Incident của
  potential parent hoặc tạo Incident nếu dữ liệu cũ chưa có.
- Response thêm `incidentId`; chưa xóa duplicate endpoint trong phase này.

## P1 — sau khi P0 ổn định

- `GET /api/user/incidents`: danh sách Incident mà người dùng đã report/follow.
- `GET /api/user/incidents/{incidentId}` và public feed theo Incident, không lặp bốn card
  cho bốn Report cùng sự vụ.
- `POST/DELETE /api/user/incidents/{incidentId}/follow`: quản lý subscription rõ ràng.
- `GET /api/staff/feedbacks/{feedbackId}/incident-candidates`: đề xuất Incident phù hợp;
  AI chỉ suggest, staff confirm khi confidence thấp hoặc có xung đột.
- API merge/split Incident với audit và cập nhật active links trong transaction.

## P2 — workflow cutover

- Thêm mutation theo Incident: status, assignment, SLA, provider report, resolution,
  approval/rework/close.
- Các route `api/management/feedbacks/{id}/...` hiện hữu dual-write hoặc forward sang
  Incident trong thời gian tương thích.
- Chỉ deprecate field/status/SLA ở Feedback sau khi telemetry cho thấy mọi Report đều có
  active Incident link và client đã chuyển contract.

## Acceptance gate P0 — đạt

- Mọi Report mới từ Web/Messenger có đúng một active Incident link; Zalo bị tắt.
- Gửi nhiều Report vào cùng Incident không tạo nhiều workflow/SLA độc lập.
- Link/unlink/confirm duplicate có event audit và chạy transaction.
- API cũ tiếp tục pass test; field thêm vào response là additive.
- Có test cho create-new, duplicate relink, unlink/promote và khóa Zalo. Concurrent link
  được bảo vệ bằng advisory lock và partial unique index; integration test concurrency
  thực trên PostgreSQL nên bổ sung ở P1.
