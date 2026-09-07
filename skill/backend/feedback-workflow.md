# Feedback Workflow — Baseline hiện tại

## Thuật ngữ đang dùng trong code

- `Feedback`: dữ liệu người dân gửi và cũng là aggregate workflow hiện tại.
- `FeedbackDuplicateCandidate`: tên legacy của đề xuất hai Report có thể cùng thuộc một
  Incident; không hàm ý xóa Report bị coi là trùng.
- `IsMasterTicket`: feedback đang là canonical master hợp lệ.
- `ParentTicketId`: feedback con đã xác nhận trùng trỏ về master.
- `FeedbackSupport`: lượt người dân bày tỏ đồng tình với một feedback công khai.
- Entity/migration `Incident` đã được apply. Contract Feedback cũ vẫn được giữ, đồng thời
  DTO trả thêm thông tin Incident và API staff Incident P0 đã hoạt động.

## Tiếp nhận

- Kênh đang tiếp nhận: `Web`, `Messenger`.
- Zalo bị tắt mặc định bằng `Zalo:Enabled=false`; controller trả `404` trước khi ghi
  inbox/queue và worker không khởi động. Chỉ bật lại khi có quyết định mới.
- Feedback mới bắt đầu ở `Submitted`.
- Mỗi Feedback mới được tạo cùng Incident, active report link, subscription và event
  trong một transaction.
- Category và priority có thể được AI bổ sung sau.
- Messenger dùng submission record để liên kết hội thoại với feedback được tạo.
- Webhook phải cô lập hội thoại theo định danh kênh và chống message replay.

## AI review và duplicate

- AI analysis và duplicate classification là hai nhánh độc lập.
- Duplicate classification tìm master cùng khu vực, có tọa độ, chưa ở trạng thái loại trừ và cũ hơn feedback mới.
- AI chỉ tạo `Pending` candidate; không tự quyết định liên kết cuối cùng.
- Mỗi feedback con chỉ có tối đa một candidate active (`Pending` hoặc `Confirmed`).
- Staff confirm/reject trong transaction.
- Confirm yêu cầu parent là master, không có parent, cùng area, cũ hơn và có trạng thái công khai hợp lệ.
- Confirm đặt `ParentTicketId`, giữ parent là master, relink Report vào Incident canonical
  và thông báo người gửi feedback con.
- Reject giữ hai active Incident riêng; candidate cuối cùng có thể nâng feedback chưa
  liên kết legacy thành master.
- Retry cùng quyết định Confirmed/Rejected là idempotent và không gửi notification lặp.

## Invariant dữ liệu

- Master không được có parent.
- Feedback không được trỏ parent về chính nó.
- Feedback có child phải tiếp tục là master công khai hợp lệ.
- Parent phải cũ hơn child; nếu cùng thời điểm dùng UUID làm tie-breaker ổn định.
- Không tạo chuỗi master → child → child; liên kết phải quy về canonical root.
- Feedback con đã link không được chạy workflow xử lý riêng.
- Duplicate review phải hoàn tất trước khi feedback chưa link đi tiếp workflow công khai.

## Workflow và SLA

- Trạng thái được chuẩn hóa qua `FeedbackStatus`, không dùng magic string mới.
- Trạng thái gồm `Submitted`, `AiReviewed`, `Verified`, `Assigned`, `InProgress`, `Resolved`, `SubmittedForApproval`, `Approved`, `Rejected`, `NeedRework`, `Closed`, `Cancelled`.
- SLA thuộc về `Incident`, không còn gắn vào từng `Feedback`. Mỗi Incident có tối đa
  một SLA `IsCurrent`.
- Thay đổi category/priority của Incident có SLA hiện hành phải recalculation.
- Chuyển trạng thái phải ghi `FeedbackStatusHistory` và đồng bộ SLA/notification.
- SLA được đồng bộ trong `IncidentService` sau khi transaction đổi trạng thái đã
  commit, vì mỗi thao tác SLA tự mở transaction riêng.
- `Submitted` và `AiReviewed` là trạng thái nội bộ, không xuất hiện trong resident feed công khai.

## Khoảng trống đã xác nhận

- Trạng thái, assignment, resolution và provider report vẫn gắn vào từng `Feedback`
  ở lớp projection; SLA đã cutover hoàn toàn sang `Incident`.
- `IsMasterTicket`/`ParentTicketId` gộp phản ánh trùng nhưng không tạo aggregate sự vụ độc lập.
- Schema mới đã biểu diễn `Report/Request → Incident` qua `IncidentReportLink`, có audit event và subscription.
- API đã dual-write nên Report mới từ Web/Messenger có đúng một active Incident link.
- Workflow ownership của status, assignment, resolution, comment và notification chưa
  cutover khỏi Feedback. SLA đã cutover (migration `MoveSlaWorkflowToIncident`).

## Incident schema foundation

- `incidents`: bản ghi sự vụ nghiệp vụ và trạng thái canonical.
- `incident_report_links`: link có trạng thái, phương thức, vai trò, confidence và audit unlink.
- `incident_events`: timeline/audit append-only của Incident.
- `incident_subscriptions`: một subscription cho mỗi cặp Incident/người dùng.
- Migration `AddIncidentAggregateSchema` backfill dữ liệu cũ theo canonical feedback root.
- Khi xóa Feedback theo API cũ, report link bị cascade; event giữ lại và bỏ tham chiếu Feedback để không phá backward compatibility.

## Incident API P0

- `GET /api/management/incidents`: queue Incident có phân trang và filter.
- `GET /api/management/incidents/{incidentId}`: canonical detail, reports, subscribers và timeline.
- `POST /api/management/incidents/{incidentId}/reports`: link Report chưa thuộc Incident khác.
- `DELETE /api/management/incidents/{incidentId}/reports/{feedbackId}`: soft-unlink và
  giữ audit history.
- Chỉ role quản lý được phép gọi; controller trả DTO, nghiệp vụ nằm trong BLL service.
- Workflow candidate có route Incident-oriented
  `/api/management/incident-match-candidates/*`; route legacy
  `/api/staff/feedback-duplicates/*` vẫn được giữ để tương thích.

## SLA aggregate

- Entity `FeedbackSla` đã đổi tên thành `IncidentSla`; bảng `feedback_slas` đổi thành
  `incident_slas`, khóa chính `feedback_sla_id` thành `incident_sla_id`.
- `incident_slas.incident_id` thay cho `feedback_id`; unique filtered index
  `ux_incident_slas_current_incident` bảo đảm mỗi Incident chỉ có một SLA hiện tại.
- `sla_events.incident_sla_id` và `sla_pause_histories.incident_sla_id` đã đổi tên
  theo, kèm index và FK tương ứng.
- Route SLA là `/api/slas/incident/{incidentId}/*`. Route `/api/slas/feedback/*` đã bị
  gỡ bỏ, đây là breaking change đã được xác nhận.
- `IncidentSlaDto` trả `IncidentId`/`IncidentTitle`; `SlaNearBreachDto`,
  `RecentSlaBreachDto` và `SlaStatusDto` trả `IncidentId`. Mọi `FeedbackSlaId`
  công khai đã đổi thành `IncidentSlaId`, gồm cả route `/api/slas/{incidentSlaId}/check`.
- SignalR event `SlaUpdated` phát `IncidentId` thay cho `FeedbackId`.
- Người dân xem được SLA của sự vụ mà họ có phản ánh đang liên kết active.
- Dashboard SLA scope thẳng theo `Incident` (`AssignedStaffUserId` cho Staff, khu vực
  phụ trách cho Manager), không đi vòng qua `IncidentReportLink`.
- Dashboard vận hành đã đổi tên thành `IncidentDashboardService` /
  `IIncidentDashboardService` / `IncidentDashboardController`, route
  `/api/incidents/dashboard/*` thay cho `/api/feedbacks/dashboard/*`. DTO nằm ở
  namespace `UrbanService.BLL.DTOs.Incident.Dashboard`.
- Dashboard tách hai nhóm chỉ số: tiếp nhận đếm `Feedback` (`TotalFeedback`,
  `NewToday`, `ReportCount`, danh sách Recent), xử lý đếm `Incident`
  (status/priority/category/area distribution, urgent open, completion rate).
- `RecentFeedbackDto` cố ý giữ tên và giữ đơn vị Report vì đó là widget tiếp nhận;
  các DTO còn lại đã đổi sang tiền tố `Incident`, `UrgentOpenFeedbackDto` thành
  `UrgentOpenIncidentDto`.
- `GET /api/incidents/dashboard/area-distribution` trả thêm `Points` là tọa độ các
  sự vụ trong khu vực (`IncidentMapPointDto`) để vẽ bản đồ, cùng `MappedCount`,
  `CenterLatitude` và `CenterLongitude`. Chỉ sự vụ có đủ vĩ độ và kinh độ mới thành
  điểm, nên `MappedCount` có thể nhỏ hơn `Count`. Query param `maxPointsPerArea`
  mặc định 500, tối đa 5000; nếu `Points.Count < MappedCount` thì danh sách đã bị cắt.
