# UrbanService — Platform Backlog

## Đang triển khai

| Năng lực | Trạng thái | Quyết định cần chốt |
|---|---|---|
| Tách Report/Request khỏi Incident | Schema + P0 API đã hoàn tất và apply DB | Tiếp tục P1 user feed/candidates/merge-split trước workflow cutover |

## Report/Request → Incident

- Report/Request là lần một người dân cung cấp thông tin về một vấn đề.
- Incident là sự vụ nghiệp vụ có thể nhận nhiều report từ nhiều người/kênh.
- `Feedback` tiếp tục là Report; `Incident` là aggregate độc lập nhận nhiều Report.
- Không đổi route/DTO Feedback hiện hữu ở phase schema foundation.
- Giữ human review cho link/merge có độ tin cậy thấp hoặc ảnh hưởng workflow.
- Kế hoạch phải bao gồm split/unlink, audit history, notification và backfill dữ liệu cũ.

## Đã chốt

- Khảo sát hệ thống civic/311/case management và tiêu chuẩn liên quan được lưu tại
  [`../research/report-incident-models/`](../research/report-incident-models/).
- Entity nền: `Incident`, `IncidentReportLink`, `IncidentEvent`, `IncidentSubscription`.
- Mỗi Report chỉ có tối đa một link `Active`; lịch sử unlink không bị ghi đè.
- Migration backfill mỗi canonical feedback root thành một Incident; feedback con trở thành report corroborating.
- Người đã gửi report được subscribe vào Incident theo cặp duy nhất `(IncidentId, UserId)`.
- Chi tiết hoàn tất nằm trong [`done/incident-schema-foundation.md`](done/incident-schema-foundation.md).
- P0 dual-write và API staff nằm trong [`done/incident-api-p0.md`](done/incident-api-p0.md).

## Việc còn mở trước workflow cutover

- Chốt ngưỡng auto-link, suggest-link và human review.
- Thêm user Incident feed/detail/follow, candidate endpoint và merge/split có audit.
- Chuyển dần status, SLA, assignment và resolution sang Incident; chưa xóa field Feedback cũ.
- Thực hiện theo [`incident-api-plan.md`](incident-api-plan.md).
