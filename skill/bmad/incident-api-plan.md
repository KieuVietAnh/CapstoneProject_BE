# Incident API Cutover Plan — Backend

Ngày cập nhật: 2026-08-26.

## 1. Mục tiêu và phạm vi

Kế hoạch này chuyển workflow vận hành từ từng `Feedback` sang `Incident` mà không làm hỏng
client đang dùng API Feedback cũ.

- `Feedback` tiếp tục là Report: người gửi, kênh gửi, nội dung gốc, vị trí và bằng chứng.
- `Incident` là aggregate sự vụ: trạng thái xử lý, phân công Staff, giao Service Provider,
  SLA, resolution, approval/rework/close và notification vận hành.
- API Incident là contract canonical cho code mới.
- API Feedback cũ chưa bị ngừng; chúng resolve active Incident rồi forward vào cùng
  application service canonical.
- Không đổi role, route hoặc response shape công khai theo hướng breaking trong kế hoạch này.
- Kế hoạch tập trung API, service orchestration, compatibility và test. Schema/model chi tiết
  phải được chốt ở story prerequisite trước khi triển khai API phụ thuộc dữ liệu mới.

## 2. Quyết định đã chốt

### 2.1 Cardinality và ownership

```text
Một Feedback ── tối đa một active link ──► một Incident
Một Incident ── có thể nhận nhiều ───────► Feedback
```

Khi mới tạo, mỗi Feedback thường có Incident riêng nên dữ liệu nhìn giống 1–1. Đây không phải
quan hệ 1–1 cố định. Sau khi xác nhận trùng, nhiều Feedback cùng trỏ active vào một Incident.

### 2.2 Xác nhận Feedback trùng

Ví dụ ban đầu:

```text
Feedback A → Incident A
Feedback B → Incident B
```

Khi staff xác nhận B cùng sự vụ với A:

```text
Feedback A ─┐
            ├─→ Incident A (canonical)
Feedback B ─┘

Incident B: Status = Merged, MergedIntoIncidentId = Incident A
```

Luồng phải thực hiện trong một transaction:

1. Soft-unlink active link `Feedback B → Incident B`.
2. Tạo active link `Feedback B → Incident A` với role `Corroborating`.
3. Chuyển subscription của B sang A và chống tạo trùng `(IncidentId, UserId)`.
4. Đánh dấu B là `Merged`, không hard-delete.
5. Ghi event ở cả Incident nguồn và đích, gồm actor, reason và ID đối ứng.
6. Đồng bộ trạng thái tương thích của Feedback B theo Incident A.

Incident B được giữ làm tombstone để bảo toàn audit, notification/link cũ, retry idempotency
và khả năng điều tra hoặc sửa match sai. Query vận hành mặc định không trả Incident đã merge.
Management detail vẫn trả `mergedIntoIncidentId`; mutation lên Incident đã merge trả conflict
kèm canonical Incident ID.

### 2.3 Trạng thái canonical

- `Incident.Status` là nguồn sự thật cho workflow vận hành.
- Mutation Incident cập nhật Incident trước, ghi `IncidentEvent`, rồi chiếu trạng thái tương
  thích xuống mọi Feedback có active link trong cùng transaction.
- API Feedback cũ không được đổi riêng một Feedback; nó resolve Incident rồi gọi chung workflow.
- Feedback mới được relink vào Incident có trước nhận trạng thái hiện tại của Incident đích;
  Feedback không được làm Incident quay lùi trạng thái.
- `Submitted` và `AiReviewed` tiếp tục là trạng thái intake của Report. Từ bước xác minh trở đi,
  trạng thái Feedback chỉ là projection tương thích của Incident.

### 2.4 Ownership field tại API boundary

| Nhóm dữ liệu | Owner canonical |
|---|---|
| Reporter, submission channel, nội dung/vị trí người dân đã gửi, attachment, comment và support | Feedback/Report |
| Title/description/location đã chuẩn hóa để điều phối, area/category canonical, priority, severity và due date vận hành | Incident |
| Status xử lý, internal Staff assignment, Service Provider, SLA, resolution và approval | Incident |
| Citizen resolution review | Feedback, nhưng phải chỉ rõ canonical Incident/resolution được đánh giá |

API update Feedback của người dân vẫn sửa Report của chính họ; không được dùng một Report mới để
ghi đè Incident đã có nhiều Report hoặc đã bắt đầu workflow. API staff cũ có DTO trộn cả hai nhóm
field nên compatibility adapter phải tách request: field Report gọi Feedback service, field vận hành
gọi Incident service, và toàn bộ validate trước khi ghi để tránh partial update.

## 3. Hiện trạng source code

### 3.1 Đã có

- Intake Web/Messenger tạo Feedback, Incident, active link, subscription và event.
- Người dùng có thể gửi thêm Report vào Incident đã tồn tại.
- Management có list/detail/update/status, link/unlink Report, internal Staff assignment,
  merge và timeline theo Incident.
- Public có Incident list/detail/reports/timeline.
- User có danh sách Incident của tôi và subscribe/unsubscribe.
- Confirm/reject duplicate có alias Incident-oriented và confirm đã relink Report vào
  Incident canonical.
- Incident nguồn rỗng sau relink/merge được giữ ở trạng thái `Merged`.

### 3.2 Chưa cutover

- `PATCH /api/management/incidents/{incidentId}/status` hiện chỉ đổi Incident; chưa áp dụng
  transition matrix đầy đủ, chưa project xuống Feedback và chưa đồng bộ SLA.
- Provider candidate/report, contact log và completion document vẫn bắt đầu từ Feedback hoặc
  `providerReportId` của Feedback.
- Submit resolution, approve và need-rework vẫn bắt đầu từ Feedback.
- Toàn bộ SLA API vẫn dùng `feedbackId`.
- Dashboard và một phần notification vẫn đo/gửi theo Feedback, có nguy cơ đếm hoặc gửi lặp khi
  một Incident có nhiều Report.
- Chưa có split Incident; merge chưa giải quyết xung đột workflow sau khi provider/SLA/
  resolution được chuyển sang Incident.

### 3.3 Readiness gap phải xử lý trước API P2

Entity Incident nền đã có, nhưng operation record hiện tại chưa incident-scoped:

- `FeedbackProviderReport` chỉ có `FeedbackId`.
- `FeedbackSla` chỉ có `FeedbackId`.
- `FeedbackResolution` chỉ có `FeedbackId`; chưa có ownership theo Incident.
- `FeedbackResolutionReview` vẫn nên Feedback-scoped, nhưng hiện chưa tham chiếu rõ canonical
  `resolutionId`/`incidentId` để chứng minh người dùng đang đánh giá kết quả nào.
- `IncidentStatus.ManagementAllowed` mới gồm `New`, `InProgress`, `Resolved`, `Closed`, trong
  khi workflow cũ còn `Verified`, `Assigned`, `SubmittedForApproval`, `Approved`,
  `NeedRework`, `Cancelled` và các trạng thái tương thích khác.

Không được triển khai API Incident bằng cách âm thầm chọn một “Feedback đại diện” để lưu provider,
SLA hoặc resolution. Cách đó tiếp tục đặt ownership lên Report và sẽ sai khi primary Report bị
unlink/merge. Story API P2 chỉ bắt đầu khi model/service contract có thể truy cập operation record
theo `IncidentId` và có invariant một current record theo đúng nghiệp vụ.

## 4. API canonical cần giữ hoặc bổ sung

### 4.1 Incident core — giữ route, hoàn thiện behavior

| Method và route | Trạng thái | Việc cần làm |
|---|---|---|
| `GET /api/management/incidents` | Đã có | Bổ sung filter workflow/provider/SLA nếu DTO hỗ trợ; mặc định loại `Merged`. |
| `GET /api/management/incidents/{incidentId}` | Đã có | Trả provider assignment hiện hành, SLA summary, current resolution và canonical target metadata khi merged. |
| `PATCH /api/management/incidents/{incidentId}` | Đã có | Recalculate provider candidates/SLA khi area, category hoặc priority đổi; ghi before/after event. |
| `PATCH /api/management/incidents/{incidentId}/status` | Đã có nhưng chưa đủ | Áp dụng transition matrix, projection Feedback, SLA và notification trong một orchestration. |
| `GET /api/management/incidents/{incidentId}/assignee-candidates` | Đã có | Giữ cho internal Staff assignment. |
| `POST /api/management/incidents/{incidentId}/assign` | Đã có | Giữ nghĩa là gán `SYSTEMSTAFF`; không dùng route này để gán Service Provider. |
| `POST /api/management/incidents/{incidentId}/merge` | Đã có | Thêm workflow-conflict guard và response canonical rõ ràng. |
| `POST /api/management/incidents/{incidentId}/split` | Chưa có | Tách danh sách Feedback sang Incident mới, giữ audit và projection đúng cho cả hai Incident. |
| `GET /api/management/incidents/{incidentId}/timeline` | Đã có | Bổ sung event provider/SLA/resolution/approval và tránh lộ dữ liệu riêng của Report khác. |

### 4.2 Giao Service Provider — thêm theo Incident

`assign` hiện hữu chỉ dành cho internal Staff. Giao bên thứ ba dùng resource riêng để tránh nhập
nhằng:

| Method và route | Mục đích |
|---|---|
| `GET /api/management/incidents/{incidentId}/provider-candidates` | Chọn provider theo area/category canonical của Incident. |
| `GET /api/management/incidents/{incidentId}/provider-reports` | Lịch sử các lần giao/chuyển provider của Incident. |
| `POST /api/management/incidents/{incidentId}/provider-reports` | Giao Incident cho provider; body gồm `coordinatorId`, `dueDate`, `note`. |

Các subresource hiện hữu tiếp tục dùng `providerReportId`, nhưng record phải thuộc Incident:

- `PATCH /api/management/provider-reports/{providerReportId}/status`.
- `GET/POST /api/management/provider-reports/{providerReportId}/contact-logs`.
- `GET/POST/DELETE /api/management/provider-reports/{providerReportId}/completion-documents`.

Quy tắc:

- `[ASSUMPTION]` Một Incident chỉ có một provider report active tại một thời điểm; các lần cũ
  được giữ làm history. Reassign tạo record mới và kết thúc record cũ, không ghi đè audit.
- Provider candidate dùng Incident area/category, không dùng một Feedback bất kỳ.
- Tạo provider report hợp lệ chuyển Incident sang `Assigned`; provider bắt đầu xử lý chuyển
  sang `InProgress`.
- Mọi linked Feedback nhận projection trạng thái, nhưng không tạo provider report riêng.
- Response DTO thêm `incidentId`; giữ các field cũ trong giai đoạn compatibility.

### 4.3 Resolution và approval — thêm theo Incident

| Method và route | Mục đích |
|---|---|
| `GET /api/management/incidents/{incidentId}/resolutions` | Lịch sử resolution của Incident. |
| `GET /api/management/incidents/{incidentId}/resolutions/{resolutionId}` | Chi tiết resolution và evidence/provider report liên quan. |
| `POST /api/management/incidents/{incidentId}/resolutions` | Staff submit kết quả; chuyển `SubmittedForApproval`. |
| `PUT /api/management/incidents/{incidentId}/resolutions/{resolutionId}/approve` | Interaction Manager duyệt đúng resolution hiện hành. |
| `PUT /api/management/incidents/{incidentId}/resolutions/{resolutionId}/need-rework` | Interaction Manager yêu cầu làm lại đúng resolution hiện hành. |

Quy tắc:

- Submit/approve/rework kiểm tra `resolutionId` thuộc Incident để tránh duyệt nhầm bản cũ.
- Rework cập nhật vòng đời của current resolution theo rule hiện hữu; không chuyển provider
  một cách ngầm định.
- Approve cập nhật Incident trước, sau đó project xuống các linked Feedback và fan-out
  notification một lần cho mỗi subscriber.
- `POST /api/user/feedbacks/{feedbackId}/resolution-review` vẫn Feedback-scoped vì mỗi người
  dân đánh giá trải nghiệm của Report mình; DTO đọc thêm `incidentId` và `resolutionId`.

### 4.4 SLA — thêm route Incident, giữ route Feedback cũ

Thêm các route song song dưới `/api/sla/incidents/{incidentId}`:

- `POST /start`.
- `GET`.
- `PATCH /responded`.
- `POST /pause`.
- `POST /resume`.
- `POST /complete`.
- `POST /recalculate`.
- `POST /cancel`.
- `GET /status`.
- `GET /timeline`.

Quy tắc:

- Một Incident chỉ có một current SLA; các SLA cũ giữ history.
- Verify/start, status transition, provider assignment và resolution phải gọi chung Incident SLA
  orchestration, không tự thay SLA ở từng controller.
- Policy selection dùng area/category/priority canonical của Incident.
- Các route `/api/sla/feedback/{feedbackId}/...` resolve active Incident rồi forward; không tạo
  SLA riêng cho Feedback.

### 4.5 Notification và area alert

- Thêm `POST /api/management/incidents/{incidentId}/notify-provider-result`; fan-out tới unique
  active subscribers, không gửi một lần cho mỗi Feedback.
- Thêm `POST /api/management/incidents/{incidentId}/area-alert`; source của alert là Incident,
  còn Report có thể được lưu như evidence/source metadata nếu cần.
- Route Feedback cũ resolve Incident rồi forward.
- Notification dùng `IncidentId`, `TargetType = Incident` và canonical URL. Nội dung không tiết
  lộ reporter hoặc evidence của Report khác.

## 5. API Feedback cũ phải chuyển thành compatibility adapter

| API hiện hữu | Behavior sau cutover |
|---|---|
| `PUT /api/management/feedbacks/{feedbackId}/verify` | Resolve active Incident → verify Incident → project mọi linked Feedback. |
| `PATCH /api/management/feedbacks/{feedbackId}/status` | Resolve active Incident → gọi Incident status workflow → trả DTO legacy. |
| `PUT /api/management/feedbacks/{feedbackId}` | Tách field Report và Incident; status/field điều phối gọi canonical Incident service, nội dung gốc vẫn cập nhật Feedback theo quyền hiện hữu. |
| `DELETE /api/management/feedbacks/{feedbackId}` | Xóa/unlink Report theo policy, sau đó xử lý Incident rỗng; không xóa Incident audit. |
| `GET /api/management/feedbacks/{feedbackId}/provider-candidates` | Resolve Incident → trả candidates của Incident. |
| `GET /api/management/feedbacks/{feedbackId}/provider-reports` | Resolve Incident → trả provider history của Incident. |
| `POST /api/management/feedbacks/assign` | Dùng `FeedbackId` để resolve Incident → tạo Incident provider report. |
| `POST /api/management/feedbacks/submit-resolution` | Resolve Incident → submit current Incident resolution. |
| `GET /api/management/feedbacks/{feedbackId}/resolutions` | Resolve Incident → trả Incident resolutions trong shape cũ. |
| `PUT /api/management/feedbacks/{feedbackId}/approve` | Resolve Incident/current resolution → approve qua canonical service. |
| `PUT /api/management/feedbacks/{feedbackId}/need-rework` | Resolve Incident/current resolution → require rework qua canonical service. |
| `POST /api/management/feedbacks/{feedbackId}/notify-provider-result` | Resolve Incident → fan-out một lần cho subscriber. |
| `POST /api/management/feedbacks/{feedbackId}/area-alert` | Resolve Incident → tạo alert từ Incident. |
| `/api/sla/feedback/{feedbackId}/...` | Resolve Incident → forward vào Incident SLA service. |

Adapter chỉ làm bốn việc: xác thực contract cũ, resolve active Incident, map request/response và
gọi canonical service. Không chứa transition rule, SLA rule hoặc notification rule riêng.

Nếu Feedback không có active Incident link, trả domain conflict có mã ổn định
`FEEDBACK_HAS_NO_ACTIVE_INCIDENT` và ghi telemetry/reconciliation queue. Không tự tạo Incident
trong một mutation vận hành vì có thể tạo thêm sự vụ sai khi dữ liệu đang lệch.

Các API attachment, comment, support và citizen review tiếp tục Feedback-scoped. Public/resident
Feedback read API được giữ cho compatibility; màn hình sự vụ mới dùng Public/User Incident API.

## 6. Workflow end-to-end cần sửa

### 6.1 Intake và duplicate review

1. Feedback mới giữ `Submitted/AiReviewed` cho intake và luôn có một active Incident link.
2. Staff xác minh qua Incident canonical; SLA Incident bắt đầu theo policy.
3. Confirm duplicate relink Report vào Incident canonical.
4. Nếu Incident đích đã đi xa hơn, Report mới nhận projection hiện tại; không rollback Incident.
5. Reject giữ hai Incident độc lập.

### 6.2 Verify → assign Staff → giao provider

1. Verify Incident và ghi event.
2. Có thể gán internal Staff qua `/assign` hiện hữu.
3. Lấy provider candidates theo Incident.
4. Tạo một provider report active cho Incident.
5. Chuyển trạng thái Incident và đồng bộ SLA/projection/notification trong transaction phù hợp.

### 6.3 Provider xử lý → submit resolution → approval

1. Provider report đổi `InProgress`; Incident chuyển `InProgress` nếu transition hợp lệ.
2. Staff upload completion evidence vào provider report.
3. Staff submit Incident resolution; Incident chuyển `SubmittedForApproval`.
4. Manager approve hoặc need-rework bằng `resolutionId` hiện hành.
5. Approve/close hoàn thành SLA và fan-out notification tới unique subscribers.
6. Need-rework resume/recalculate SLA theo rule đã chốt và giữ audit vòng trước.

### 6.4 Merge sau khi workflow đã bắt đầu

Merge/relink đơn giản chỉ được tự động khi Incident nguồn chưa có operation record cần giải quyết.

`[ASSUMPTION]` Nếu Incident nguồn và đích có active provider assignment, current SLA hoặc current
resolution xung đột, API không âm thầm chọn một bên. Trả `409 INCIDENT_MERGE_WORKFLOW_CONFLICT`
kèm summary xung đột để staff thực hiện merge management với chiến lược rõ ràng ở story riêng.

Không copy hai current SLA hoặc hai active provider assignment vào Incident đích. Sau merge,
Incident nguồn là `Merged`; mutation lên nguồn trả conflict kèm `mergedIntoIncidentId`.

### 6.5 Split và sửa match sai

1. Staff chọn danh sách Feedback cần tách và cung cấp reason.
2. API tạo Incident mới, soft-unlink link cũ và tạo active link mới trong transaction.
3. Subscription theo source Feedback được chuyển phù hợp; subscription manual cần policy riêng.
4. Incident status mới được xác định bằng strategy explicit, không suy ra từ Feedback tùy ý.
5. Provider/SLA/resolution không được nhân bản ngầm; nếu có operation data, trả conflict để staff
   chọn strategy.

### 6.6 Sửa hoặc xóa Report

- Người dân sửa nội dung Report chỉ thay đổi Feedback. Nếu Report là nguồn duy nhất và Incident
  chưa được staff xác minh, service có thể đồng bộ lại snapshot canonical theo policy đã chốt;
  ngoài điều kiện đó phải tạo event/candidate review thay vì ghi đè Incident tự động.
- Xóa attachment/comment/support chỉ tác động Report và không đổi Incident workflow.
- Khi xóa hoặc withdraw một Report, soft-unlink trước và cập nhật subscription bắt nguồn từ Report.
- Nếu Incident còn Report active, giữ Incident và bảo đảm có đúng một `Primary` link.
- Nếu Incident không còn Report nhưng đã có event/workflow vận hành, giữ tombstone theo trạng thái
  phù hợp; không hard-delete audit.
- `[ASSUMPTION]` Nếu Incident chưa từng được công khai và chưa bắt đầu workflow, cleanup vật lý chỉ
  được thực hiện bởi maintenance policy riêng, không nằm trong request xóa Feedback thông thường.

## 7. Thứ tự triển khai đề xuất

Mỗi lát cắt phải gồm route Incident, canonical service, Feedback adapter, audit, notification và test;
không triển khai route mới đơn lẻ.

### Slice 0 — Contract và readiness gate

- Cập nhật Swagger/DTO với `incidentId`, canonical/merged metadata và error code ổn định.
- Chốt Incident transition matrix dựa trên workflow hiện hữu; `Merged` chỉ do hệ thống đặt.
- Hoàn tất model prerequisite cho provider/SLA/resolution theo Incident.
- Thêm resolver dùng chung `FeedbackId → active IncidentId` và kiểm tra invariant.

### Slice 1 — Status, verify và projection

- Hoàn thiện `PATCH /management/incidents/{id}/status`.
- Thêm hoặc chuẩn hóa verify Incident trong canonical service.
- Dual-write trạng thái tương thích tới tất cả active linked Feedback.
- Forward `feedbacks/{id}/verify`, `feedbacks/{id}/status` và staff update có field status.
- Tách field ownership trong `PUT /management/feedbacks/{id}`; không để request hỗn hợp ghi dở dang.
- Ghi Incident event và Feedback status history với correlation chung; notification de-duplicate.

### Slice 2 — Provider assignment

- Thêm Incident provider-candidates/provider-reports API.
- Chuyển provider status/contact/evidence sang Incident-owned record.
- Forward toàn bộ provider API bắt đầu từ Feedback.
- Thêm reassign history và guard một active provider report.

### Slice 3 — Resolution và approval

- Thêm Incident resolution list/detail/submit/approve/need-rework.
- Forward các API Feedback tương ứng.
- Gắn evidence/provider report đúng Incident và kiểm tra current resolution.
- Giữ citizen review Feedback-scoped nhưng tham chiếu canonical resolution.

### Slice 4 — SLA

- Thêm Incident SLA routes và canonical Incident SLA service methods.
- Forward toàn bộ Feedback SLA routes.
- Chuyển SLA worker/dashboard sang đếm current Incident SLA, không đếm từng Report.
- Kiểm tra recalculate khi Incident area/category/priority đổi.

### Slice 5 — Merge/split hardening và fan-out

- Thêm merge conflict guard sau workflow cutover.
- Thêm split API và audit.
- Chuẩn hóa response cho merged Incident.
- Chuyển provider-result notification và area alert sang Incident; chống gửi lặp subscriber.

### Slice 6 — Read path, dashboard và deprecation telemetry

- Incident detail trả đủ workflow summary.
- Dashboard tách `Reports received` và `Unique incidents`.
- Gắn warning/deprecation metadata trong Swagger cho route Feedback mutation cũ, chưa xóa route.
- Theo dõi số lần gọi legacy route, missing active link, projection drift và merge conflict.

## 8. Error contract và concurrency

Các lỗi nghiệp vụ mới cần mã ổn định trong error response hiện hữu:

- `FEEDBACK_HAS_NO_ACTIVE_INCIDENT`.
- `INCIDENT_MERGED`, kèm `mergedIntoIncidentId`.
- `INCIDENT_STATUS_TRANSITION_INVALID`.
- `INCIDENT_PROVIDER_ALREADY_ACTIVE`.
- `INCIDENT_RESOLUTION_NOT_CURRENT`.
- `INCIDENT_MERGE_WORKFLOW_CONFLICT`.
- `INCIDENT_REPORT_LINK_CONFLICT`.

Mutation relink/merge/split/provider assignment/submit/approve phải chạy transaction. Dùng cơ chế
lock/unique invariant hiện hữu để chống hai request tạo hai active link, current SLA, active provider
report hoặc current resolution. Retry cùng intent không được tạo event hoặc notification lặp.

## 9. Test và acceptance gate

### Unit/API tests bắt buộc

- Route/role cho từng Incident API mới và Feedback adapter cũ.
- Cùng một intent qua route Incident và route Feedback cho cùng kết quả canonical.
- Đổi Incident status project tới mọi active linked Feedback, không đụng Feedback đã unlinked.
- Feedback relink vào Incident tiến xa hơn nhận projection mà không rollback Incident.
- Một Incident có nhiều Report chỉ tạo một provider assignment, current SLA và current resolution.
- Provider candidate lấy area/category từ Incident.
- Approve/rework từ Feedback bất kỳ trong Incident tác động cùng current resolution.
- Notification gửi một lần cho mỗi unique subscriber.
- Merged Incident không nhận mutation và trả canonical target.
- Staff update Feedback hỗn hợp map đúng field Report/Incident và rollback toàn bộ khi một phần lỗi.
- Xóa/withdraw Report cuối không xóa Incident đã có audit hoặc workflow.
- Merge có workflow conflict trả `409`; không mất operation data.
- Legacy DTO/route giữ response shape tương thích.

### PostgreSQL integration tests

- Hai request link/relink đồng thời không tạo hai active Incident link cho một Feedback.
- Hai request assign provider đồng thời không tạo hai active provider report.
- Hai request submit/approve đồng thời không tạo hai current resolution hoặc notification lặp.
- Transaction rollback không để Incident/Feedback status, SLA và event lệch nhau.

### Lệnh kiểm tra trước bàn giao mỗi slice

```powershell
dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj
dotnet build UrbanService.sln
dotnet ef migrations has-pending-model-changes --project UrbanService.DAL --startup-project UrbanService
git diff --check
```

Không báo hoàn tất slice nếu canonical API pass nhưng compatibility adapter hoặc projection test
chưa pass.

## 10. Rollout và rollback

- Bật canonical Incident workflow theo feature flag/config cho từng slice.
- Trong giai đoạn chuyển tiếp, giữ projection Feedback để client cũ đọc đúng.
- So sánh telemetry Incident/Feedback status, SLA và notification trước khi mở toàn bộ traffic.
- Rollback bằng cách tắt route orchestration mới và quay adapter về behavior cũ chỉ khi projection
  vẫn còn đầy đủ; schema additive và event audit không bị xóa.
- Chỉ lập kế hoạch ngừng route Feedback sau khi telemetry cho thấy client đã chuyển và không còn
  projection drift. Việc xóa route/field là một breaking-change project riêng.

## 11. Việc cần chốt trước khi build các slice phụ thuộc

- `[ASSUMPTION]` Một Incident chỉ có một provider report active; xác nhận nếu nghiệp vụ cho phép
  nhiều provider xử lý song song.
- Chốt strategy cho merge/split khi cả hai Incident đã có provider/SLA/resolution.
- Chốt transition matrix đầy đủ của Incident và mapping sang status Feedback legacy.
- Chốt chuẩn response conflict theo middleware hiện hữu để FE xử lý canonical redirect ổn định.

## 12. Trạng thái phase

- P0 schema/intake/linking: hoàn tất, xem [`done/incident-api-p0.md`](done/incident-api-p0.md).
- Incident match workflow: hoàn tất, xem
  [`done/incident-match-workflow.md`](done/incident-match-workflow.md).
- P1 public/user/management read và một phần operation: phần lớn đã có trong code; tài liệu cần
  kiểm chứng theo từng acceptance test trước khi đóng phase.
- P2 workflow cutover: đang triển khai.
  - Slice 1 status/verify/projection đã triển khai ngày 2026-08-26: API Feedback cũ forward
    mutation vận hành vào Incident; status Incident được chiếu xuống tất cả active linked Feedback;
    relink xác nhận trùng đồng bộ theo Incident đích và retry có thể sửa projection bị lệch.
  - `Submitted`/`AiReviewed` vẫn thuộc intake Feedback; rollback Incident về `New` bị chặn.
  - Slice 2–6 chưa triển khai. Slice 2 cần chốt một hay nhiều provider assignment active trên mỗi
    Incident trước khi tạo contract/schema tương ứng.
