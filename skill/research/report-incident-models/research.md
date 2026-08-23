---
title: 'Nghiên cứu kỹ thuật: Report/Request và Incident trong xử lý phản ánh đô thị'
type: 'technical'
topic: 'Report/Request–Incident models for civic issue management'
decision: 'Chọn mô hình dữ liệu và lộ trình áp dụng cho UrbanService'
source: 'native-web-research'
status: complete
preset: 'standard'
validation: 'normal'
claims_verified: 7
claims_unverified: 1
created: '2026-08-23'
updated: '2026-08-23'
---

# Report/Request và Incident trong xử lý phản ánh đô thị

**Quyết định nghiên cứu phục vụ:** Chọn mô hình dữ liệu và lộ trình chuyển UrbanService
từ master-feedback sang phân tách Report/Request và Incident.

## Tóm tắt điều hành

UrbanService nên tạo entity `Incident` độc lập và giữ `Feedback` hiện tại như
`Report` trong giai đoạn chuyển đổi. Quan hệ mục tiêu là `Incident 1 — N Report`;
status xử lý, assignment, resolution và resolution SLA thuộc Incident, còn người gửi,
kênh, nội dung gốc, bằng chứng và acknowledgement thuộc Report.

Không nên chỉ đổi tên master feedback thành Incident. Cách đó vẫn để workflow nằm trên
một phản ánh ngẫu nhiên của người dân, gây nhập nhằng khi master bị rút, sửa nội dung,
đóng duplicate hoặc cần tách nhóm. Open311 GeoReport v2 và NYC311 vẫn request-centric,
nhưng ví dụ duplicate cho thấy request có thể đóng trong khi vấn đề gốc còn mở [1][3].
SeeClickFix/CivicPlus có bằng chứng vận hành về merge request trùng vào một open case;
việc gọi đây là canonical case là diễn giải kiến trúc từ hành vi đó [8][9]. ServiceNow
cung cấp mẫu tham khảo tách Case khỏi Incident/Request vận hành [11][12].

Khuyến nghị triển khai theo migration additive và dual-write, không big-bang. Backfill
mỗi cluster master/child hiện hữu thành một Incident; feedback độc lập thành Incident
riêng; pending candidate chưa được gộp. Chỉ chuyển staff workflow sang Incident sau khi
shadow-read và đối soát dữ liệu đạt yêu cầu.

Caveat lớn nhất: không có chuẩn civic duy nhất cho “Incident”. GeoReport v2 không định
nghĩa `incident_id`, `duplicate_of` hay canonical relation [1]. Vì vậy terminology và
invariants của UrbanService phải được chốt bằng ADR/PRD, không map theo tên bảng vendor.

## 1. Các mô hình thực tế

### Open311, NYC311 và DC311: request-centric

Open311 GeoReport v2 chuẩn hóa service type và service request. `service_request_id` là
ID của request; token chỉ hỗ trợ tạo request bất đồng bộ. Spec không có `incident_id`,
`duplicate_of` hoặc canonical issue relation [1]. NYC311 cũng cấp số và SLA theo từng
service request; một request có thể bị đóng vì đã có original complaint đang xử lý
[2][3], trong khi dataset công khai vẫn lưu dữ liệu theo từng SR [4].

Điểm rút ra (suy luận kiến trúc): GeoReport v2 hữu ích làm contract
intake/interoperability, nhưng không đủ làm domain model Incident. `service_request_id`
nên map vào Report/Case phía công dân, không mặc định map vào Incident.

### FixMyStreet và Snap Send Solve: ngăn duplicate trước submit

FixMyStreet hiển thị report gần đó và hướng người dân subscribe report cũ thay vì tạo
report mới [5]. Staff vẫn có thể đóng một report đã tạo với trạng thái duplicate [6].
Core schema công khai dùng một `problem` cho mỗi report cùng comment và alert
subscription; schema không biểu diễn rõ canonical issue table hay duplicate FK [7].
Snap Send Solve cũng có thể đưa ra candidate; khi người dùng chọn vấn đề đã có, hệ thống
ghi nhận deflection và không tạo report mới. Nếu report công khai, user có thể lưu nó để
nhận cập nhật [10].

Điểm rút ra: luồng “vấn đề này đã có — theo dõi thay vì gửi lại” giảm tải tốt, nhưng
không thay thế mô hình nhiều Report cùng Incident khi report mới mang thêm người gửi,
bằng chứng hoặc kênh cần audit.

### SeeClickFix/CivicPlus: canonicalization có bằng chứng production

CivicPlus công bố khả năng phát hiện và merge duplicate submissions, đồng thời route và
assign request theo location/category [8]. Một record production của City of Tacoma cho
thấy request trùng được add vào open case, liên kết về request gốc và người theo dõi được
chuyển sang nhận update từ canonical issue [9].

Điểm rút ra (suy luận kiến trúc): đây là mô hình gần nhất với góp ý review —
report/request vẫn có ID và lineage riêng, còn công việc vận hành quy về issue/case đích.

### ServiceNow và Dynamics: tách giao tiếp khỏi vận hành

ServiceNow phân biệt Case, Case Task và Incident/Request. Một Case có thể tạo hoặc liên
kết tới Incident/Request; Incident/Request giữ related list các Customer Cases [11][12].
Dynamics gắn SLA và assignment vào Case/queue; khi merge, activity, email, attachment
được reassociate và child case được reparent sang record đích [13][14][15].

Điểm rút ra (suy luận thiết kế): status giao tiếp và status khắc phục không nên là một.
Tuy nhiên UrbanService chưa cần thêm entity `Case` riêng ngay; `Feedback` có thể đóng vai
Report/Case phía công dân, còn `Incident` là aggregate vận hành.

## 2. Đối chiếu với UrbanService hiện tại

### Đã có nền tốt

- `Feedback` giữ người gửi, kênh, nội dung, vị trí và bằng chứng.
- `FeedbackDuplicateCandidate` đã có `Pending/Confirmed/Rejected`, confidence, reason và human reviewer.
- `ParentTicketId`/`IsMasterTicket` đã có canonical-root invariant, transaction và PostgreSQL constraint/trigger.
- API đã trả master/linked feedback và background worker đã tách AI analysis khỏi duplicate classification.

### Khoảng lệch kiến trúc

- `Feedback` vừa là report vừa là đơn vị xử lý.
- Status history, SLA, assignment/provider report, resolution và approval đều mang `FeedbackId`.
- Master là một feedback do người dân gửi, không phải aggregate trung lập.
- Child feedback bị chặn workflow riêng nhưng lifecycle vẫn tồn tại trên cùng bảng.
- `Closed` của duplicate và `Resolved/Closed` của sự vụ không được tách semantic rõ ràng.

Phạm vi ảnh hưởng trực tiếp gồm `FeedbackSla`, `FeedbackStatusHistory`,
`FeedbackProviderReport`, `FeedbackResolution`, `FeedbackResolutionReview`,
`CompletionDocument`, dashboard, notification và DTO/API đang trả `FeedbackId`.

## 3. Mô hình mục tiêu đề xuất

```text
User/Channel
    │
    ▼
Report (Feedback hiện tại) ── N:1 ── Incident
    │                           │
    ├─ raw content/evidence     ├─ normalized location/category/priority
    ├─ reporter/channel         ├─ operational status/assignment
    ├─ acknowledgement         ├─ resolution SLA/provider/resolution
    └─ comments/interactions    └─ public updates/event history
                IncidentReportLink
          (method, confidence, reason, actor, time)
```

### Phân bổ dữ liệu

| Thuộc Report | Thuộc Incident | Thuộc quan hệ/subscription |
|---|---|---|
| User, channel, raw title/description | Canonical title/summary | Link method: user/staff/AI/backfill |
| Raw location và evidence attachment | Verified location, area, category | Confidence, reason, linked by/at |
| Submission/conversation IDs | Priority và operational status | Active/unlinked/split lineage |
| Created time, privacy, withdrawal | Assignment, provider, resolution | Reporter/follower notification |
| Intake/acknowledgement status | Resolution SLA và incident history | Support/follow source |

### Các entity tối thiểu

1. `Incident`: aggregate vận hành độc lập.
2. `IncidentReportLink`: liên kết active duy nhất cho mỗi Report, có audit metadata.
3. `IncidentEvent`: ledger append-only cho link/unlink/merge/split/status/assignment.
4. `IncidentSubscription`: người nhận update, tách khỏi việc bắt buộc tạo Report.
5. `IncidentMatchCandidate`: đích là Incident; có thể chuyển dần từ `FeedbackDuplicateCandidate`.

Không hard-delete khi merge. Incident nguồn chuyển `Merged`, lưu `MergedIntoIncidentId`;
Report link được chuyển trong transaction và lịch sử nguồn vẫn truy được. Split cũng là
thao tác có event, không sửa/xóa lịch sử cũ.

## 4. Chính sách đối sánh và trùng lặp

### Ba đường vào

1. **Exact replay/idempotency:** cùng message ID/submission key → không tạo Report lần hai.
2. **User-selected existing incident:** user thấy candidate và chọn “đúng sự vụ này” → tạo subscription; nếu gửi thêm bằng chứng thì vẫn tạo Report và link ngay.
3. **AI/staff matching:** hệ thống tạo candidate; staff confirm/reject trước khi đổi ownership workflow.

### Tạo candidate

- Hard filter: cùng/giáp area, category tương thích, Incident chưa terminal hoặc trong reopen window.
- Geo ranking: khoảng cách theo loại sự vụ và độ chính xác tọa độ.
- Time ranking: window khác nhau cho chó chạy rong, ổ gà, rác tồn đọng hoặc mất điện.
- Semantic ranking: title/description đã normalize; ảnh chỉ là tín hiệu phụ.
- Không auto-merge chỉ vì similarity score cao.

### Trường hợp biên bắt buộc

- Cùng vị trí/category nhưng là hai lần tái diễn ở thời điểm khác.
- Report mới đến sau khi Incident `Resolved/Closed`: reopen hay tạo Incident mới theo policy.
- Link sai cần unlink/split mà không mất notification hoặc evidence.
- Merge tạo vòng; unique active link và canonical target phải được khóa transaction.
- Reporter rút một Report nhưng Incident còn Report khác.
- Không lộ danh tính/nội dung riêng của reporter khác trong incident public view.

## 5. Trạng thái, SLA và thông báo

### Trạng thái

- Report intake: `Submitted`, `AiReviewed`, `Linked`, `Rejected`, `Withdrawn`.
- Incident operation: `New`, `Verified`, `Assigned`, `InProgress`, `Resolved`, `AwaitingApproval`, `Approved`, `NeedRework`, `Closed`, `Cancelled`, `Merged`.
- Giữ legacy `Feedback.Status` trong giai đoạn compatibility; trả status dẫn xuất từ Incident sau cutover.

### SLA

- Acknowledgement/first-response SLA tính theo từng Report vì mỗi người báo ở thời điểm khác nhau.
- Resolution SLA tính một lần trên Incident và không reset khi report mới được link.
- Link report mới vào Incident phải gửi ngay trạng thái hiện tại, không tạo resolution SLA mới.

### Thông báo

- Người tạo Report tự động subscribe Incident sau khi link.
- Incident update fan-out theo `IncidentSubscription`, có outbox/idempotency key.
- Merge chuyển subscription sang canonical target và chống gửi trùng.
- Split chỉ chuyển những subscription gắn với Report được tách hoặc yêu cầu user chọn lại.

## 6. Lộ trình áp dụng

### Phase 0 — Chốt contract nghiệp vụ

- Phê duyệt glossary: Report/Request, Incident, Duplicate, Follow/Support, Merge/Split/Reopen.
- Viết ADR chốt ownership table và state machine.
- Chốt public privacy: người dân thấy Incident nhưng chỉ thấy Report của mình và evidence công khai.

### Phase 1 — Schema additive

- Thêm `incidents`, `incident_report_links`, `incident_events`, `incident_subscriptions`.
- Thêm index/constraint: một active Incident/Report, no self-merge, canonical merge target, geo/status lookup.
- Giữ nguyên `feedbacks.parent_ticket_id`, `is_master_ticket` và API hiện tại.

### Phase 2 — Backfill và dual-write

- Mỗi cluster master + confirmed children → một Incident.
- Feedback độc lập → một Incident riêng.
- Pending candidate giữ Incident riêng; không gộp trước khi staff quyết định.
- Ghi bảng reconciliation: legacy master, incident target, report count và lỗi.
- Dual-write link mới sang cả legacy duplicate relation và Incident relation dưới feature flag.

### Phase 3 — Read path và UX

- Bổ sung `IncidentId`, `IncidentStatus`, `ReportCount`, `IsFollowing` vào DTO theo cách tương thích.
- Thêm resident incident feed/detail; route feedback cũ vẫn hoạt động.
- Trước submit hiển thị candidate với hai lựa chọn: theo dõi hoặc gửi thêm bằng chứng.
- Staff dashboard đếm cả `Reports received` và `Unique incidents`.

### Phase 4 — Chuyển workflow vận hành

- Staff verify/assign/process/resolve trên Incident.
- Chuyển provider report, resolution, approval, operational status history và resolution SLA sang Incident.
- Feedback status trở thành intake/derived compatibility status.
- Shadow compare dashboard/SLA/notification trước khi bật write chính thức.

### Phase 5 — Merge, split và cleanup

- Mở API staff link/unlink/merge/split/reopen có optimistic concurrency và audit.
- Ngừng dual-write sau thời gian ổn định và đối soát không lệch.
- Chỉ bỏ `ParentTicketId`/`IsMasterTicket` khi không còn consumer; migration cleanup tách riêng.

## 7. API đề xuất

- Giữ `POST /api/user/feedbacks`; service nội bộ tạo Report và Incident/link theo policy.
- `GET /api/user/incidents` và `GET /api/user/incidents/{id}` cho resident view.
- `GET /api/management/incidents` và `GET /api/management/incidents/{id}/reports`.
- `POST /api/management/incidents/{incidentId}/reports/{reportId}/link`.
- `POST /api/management/incidents/{incidentId}/reports/{reportId}/unlink`.
- `POST /api/management/incidents/{sourceId}/merge/{targetId}`.
- `POST /api/management/incidents/{incidentId}/split` với danh sách Report cần tách.
- Mutation yêu cầu reason, actor, expected version và trả canonical redirect khi merged.

## 8. Verification và rollout gates

- Backfill invariant: tổng số Report trước/sau bằng nhau; mỗi Report có đúng một active link.
- Cluster invariant: confirmed legacy child cùng Incident với legacy master.
- Workflow invariant: một Incident chỉ có một current resolution SLA và một active assignment.
- Privacy test: user không xem được reporter/evidence private của Report khác.
- Concurrency test: hai staff confirm/merge đồng thời không tạo hai active links hoặc merge cycle.
- Notification test: một incident event chỉ gửi một lần cho mỗi subscriber.
- Metrics: report/incident ratio, candidate confirm/reject, false-link/unlink, merge/split, pending age, resolution SLA.
- Rollback: tắt feature flag và quay read path về legacy; schema additive giữ dữ liệu để điều tra.

## 9. Khuyến nghị

Chọn **Incident entity riêng + Report giữ nguyên**. Không chọn:

- Chỉ đổi tên master feedback: không giải quyết ownership và merge/split.
- Chặn mọi report trùng: làm mất bằng chứng, reporter và audit của phản ánh mới.
- Mô hình bốn entity Submission–Case–Incident–Task ngay lập tức: đúng ở hệ thống lớn nhưng quá nặng cho baseline hiện tại.

Đây là thay đổi cross-module và schema lớn. Bước kế tiếp hợp lý là tạo PRD/architecture
spine từ research này, chưa bắt đầu migration cho tới khi Phase 0 được duyệt.

## Open questions

1. `Feedback` sẽ đổi tên domain thành `Report` trong code hay chỉ đổi nhãn UI/API mới?
2. Có cần giữ acknowledgement SLA riêng cho từng Report hay chỉ acknowledgement tức thời?
3. Chính sách reopen window theo từng category là bao lâu?
4. User chọn incident có sẵn nhưng không gửi evidence sẽ tạo `Support`, `Subscription` hay cả hai?
5. Incident public view cho phép chia sẻ attachment/comment nào từ Report?

## Source appendix

| Ref | Finding | Publisher | Pub date | Accessed | Confidence |
|---|---|---|---|---|---|
| [1] | GeoReport v2 chuẩn hóa service request, ID, token, status; không có Incident relation | [Open311](https://wiki.open311.org/GeoReport_v2/) | n.d. | 2026-08-23 | High |
| [2] | Service Request và follow behavior | [NYC311](https://portal.311.nyc.gov/article/?kanumber=KA-03116) | n.d. | 2026-08-23 | High |
| [3] | Duplicate SR vẫn có ID riêng | [NYC311](https://portal.311.nyc.gov/sr-details/?id=849ef0c5-a62f-ec11-b76a-2818785c9413) | n.d. | 2026-08-23 | High/Medium |
| [4] | Row-per-SR schema và SLA fields | [NYC Open Data](https://data.cityofnewyork.us/api/views/erm2-nwe9) | 2025-12-23 | 2026-08-23 | High |
| [5] | Gợi ý report gần đó và subscribe trước submit | [FixMyStreet Pro](https://fixmystreet.org/pro-manual/print/) | n.d. | 2026-08-23 | High |
| [6] | Staff có thể đóng report duplicate | [FixMyStreet](https://fixmystreet.org/running/admin_manual/) | n.d. | 2026-08-23 | High |
| [7] | Core schema problem/comment/alert không biểu diễn rõ canonical issue table/duplicate FK | [mySociety](https://raw.githubusercontent.com/mysociety/fixmystreet/master/db/schema.sql) | n.d. | 2026-08-23 | High/Medium |
| [8] | Detect/merge duplicate, route/assign theo location/category | [CivicPlus](https://www.civicplus.com/seeclickfix-311-crm/unified-platform/) | n.d. | 2026-08-23 | High/Medium |
| [9] | Production duplicate được add vào canonical open case | [City of Tacoma/SeeClickFix](https://seeclickfix.com/web_portal/Mx4UcnjshtJ83uMYFA2D58p5/issues/map/21403197) | 2026-04-21 | 2026-08-23 | High |
| [10] | Candidate/deflection không tạo report mới | [Snap Send Solve](https://help.snapsendsolve.com/en/articles/11560117-how-are-duplicate-reports-handled) | 2025-06-12 | 2026-08-23 | High |
| [11] | Case là hồ sơ chính, có Case Task và Related Parties | [ServiceNow](https://www.servicenow.com/docs/r/customer-service-management/csm-cases-case-tasks-overview.html) | 2026-03-12 | 2026-08-23 | High |
| [12] | Case liên kết Incident/Request và quan hệ hai chiều | [ServiceNow](https://www.servicenow.com/docs/r/customer-service-management/csm-item-agent-tasks.html) | 2026-03-12 | 2026-08-23 | High |
| [13] | Merge Case theo target; reassociate activity/email/attachment và reparent child case | [Microsoft](https://learn.microsoft.com/en-us/dynamics365/customer-service/use/customer-service-hub-user-guide-merge-cases) | 2026-05-08 | 2026-08-23 | High |
| [14] | SLA gắn với Case record | [Microsoft](https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/apply-slas?tabs=customerserviceadmincenter) | 2025-05-02 | 2026-08-23 | High |
| [15] | Queue/user/team assignment cho Case/activity | [Microsoft](https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/set-up-queues-manage-activities-cases) | 2026-05-29 | 2026-08-23 | High |

## Staleness map

| Claim class | Re-check by | Note |
|---|---|---|
| Dynamics merge behavior | 2026-11-08 | Vendor behavior window 6 tháng |
| CivicPlus product behavior | 2027-02-23 | Vendor behavior window 6 tháng |
| Municipal production behavior | 2027-04-21 | Tacoma record là bằng chứng thời điểm cụ thể |
| ServiceNow architecture pattern | 2028-03-12 | Pattern window 24 tháng |
| Open311 stable standard | 2028-08-23 | Spec frozen/finalized; kiểm tra nếu dự án chọn interoperability |

Mốc cần kiểm tra sớm nhất theo `recon_kit.py staleness`: **2026-11-08**.
