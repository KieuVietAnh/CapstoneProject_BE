# Digest: mô hình Case / Incident / Request cho phản ánh đô thị

Accessed: 2026-08-23
Phạm vi: tài liệu triển khai/schema chính thức; 8 nguồn; không dùng project context làm bằng chứng.

## Kết luận ngắn

Pattern được chứng minh tốt nhất là tách **bản gửi/tương tác** khỏi **Case**, và tách **Case** khỏi **Incident/Request vận hành**. Một Case là hồ sơ hướng người dân: gom bối cảnh, trao đổi, cam kết và kết quả trả lời. Interaction/submission là từng lần liên hệ qua kênh. Incident là sự kiện vận hành cần khôi phục/xử lý; Request là yêu cầu cung cấp dịch vụ. Nhiều interaction và nhiều related party có thể gắn với một Case; nhiều Case có thể quy về một Incident vận hành. Đây là inference kiến trúc dựa trên schema và workflow của ServiceNow/Dynamics, không phải chuẩn thuật ngữ phổ quát.

## Verified facts

1. **Case là hồ sơ chính hướng khách hàng, không chỉ là một message.** ServiceNow định nghĩa Case là record chính của CSM, lưu thông tin khách hàng, câu hỏi/vấn đề, công việc giải quyết; activity pane giữ lịch sử trao đổi, notes, updates và attachments.
   - source: https://www.servicenow.com/docs/r/customer-service-management/csm-cases-case-tasks-overview.html
   - publisher: ServiceNow; pub_date: 2026-03-12; accessed: 2026-08-23; confidence: high; class: data-model/lifecycle

2. **Interaction là đơn vị giao tiếp/kênh, có lifecycle riêng và có thể liên kết sang Case.** Với email reply tham chiếu một interaction đã đóng, ServiceNow không mở lại interaction; nó liên kết reply sang interaction mở hoặc Case mở phù hợp. Threading dùng watermark/reference ID.
   - source: https://www.servicenow.com/docs/r/customer-service-management/email-reply-routing-closed-interactions.html
   - publisher: ServiceNow; pub_date: 2026-05-11; accessed: 2026-08-23; confidence: high; class: interaction-linking/dedup

3. **Một Case hỗ trợ nhiều contact/consumer với quyền khác nhau.** ServiceNow gọi các chủ thể bổ sung này là related parties và lưu trong related list của Case; Case vẫn có contact/consumer chính.
   - source: https://www.servicenow.com/docs/r/customer-service-management/csm-cases-case-tasks-overview.html
   - publisher: ServiceNow; pub_date: 2026-03-12; accessed: 2026-08-23; confidence: high; class: relationship/cardinality

4. **Case, Incident và Request là các record khác nhau trong ServiceNow.** Từ một Case mở, agent có thể tạo hoặc liên kết Incident/Request. Case→Incident copy mô tả, impact/urgency, contact→caller, CI; Incident giữ related list “Customer Cases”. Case→Request copy mô tả/contact và liên kết hai chiều.
   - source: https://www.servicenow.com/docs/r/customer-service-management/csm-item-agent-tasks.html
   - publisher: ServiceNow; pub_date: 2026-03-12; accessed: 2026-08-23; confidence: high; class: data-model/integration

5. **Thuật ngữ vendor xung đột: Dynamics Case có logical/schema name `incident`.** Tài liệu SLA minh họa cập nhật SLA cho Case bằng `incident["slaid"]`; vì vậy không được suy ra rằng bảng `incident` của Dynamics mang nghĩa ITSM Incident như ServiceNow.
   - source: https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/apply-slas?tabs=customerserviceadmincenter
   - publisher: Microsoft; pub_date: 2025-05-02; accessed: 2026-08-23; confidence: high; class: terminology/schema

6. **SLA được gắn với record nghiệp vụ (đặc biệt Case), không với từng message.** Dynamics cho phép áp SLA qua workflow/Power Automate/plugin, entitlement, thủ công hoặc default; tại một thời điểm chỉ một SLA áp vào record, đổi SLA sẽ cancel SLA trước; reevaluation tạo SLA KPI instance mới và cancel instance trước.
   - source: https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/apply-slas?tabs=customerserviceadmincenter
   - publisher: Microsoft; pub_date: 2025-05-02; accessed: 2026-08-23; confidence: high; class: SLA/lifecycle

7. **Assignment là trách nhiệm xử lý queue/user/team, tách khỏi dữ liệu trao đổi.** Dynamics queues chứa Case hoặc activity; work có thể được route tự động/thủ công, rồi agent nhận hoặc manager gán cho user/team. Khi release, record quay về queue owner.
   - source: https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/set-up-queues-manage-activities-cases
   - publisher: Microsoft; pub_date: 2026-05-29; accessed: 2026-08-23; confidence: high; class: ownership/assignment

8. **Merge cần một record đích và phải bảo tồn quan hệ.** Dynamics cho phép merge tối đa 10 Case: nguồn chuyển canceled/merged; activities, emails, attachments sang Case đích; merged cases còn thấy ở Case Relationships.
   - source: https://learn.microsoft.com/en-us/dynamics365/customer-service/use/customer-service-hub-user-guide-merge-cases
   - publisher: Microsoft; pub_date: 2026-05-08; accessed: 2026-08-23; confidence: high; class: duplicate/merge

9. **Salesforce cũng dùng master-case merge nhưng semantics khác.** Có thể merge tối đa 3 Case; related lists, feed items và child records sang master. Non-master được soft-delete hoặc giữ lại với trạng thái/nhãn merged tùy cấu hình.
   - source: https://help.salesforce.com/s/articleView?id=sf.cases_merge.htm&language=en_US&type=5
   - publisher: Salesforce; pub_date: n.d.; accessed: 2026-08-23; confidence: medium; class: duplicate/merge

10. **Audit phải cấu hình theo scope và có khoảng trống cần bù.** Dynamics cho phép audit ở environment/table/column và liệt kê queue, routing rules, SLA, entitlement, Case (`incident`) trong audit events; nhưng tự động đưa thành viên team hiện có vào private queue không được audit log ghi lại.
   - sources: https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/enable-audit-tables ; https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/set-up-queues-manage-activities-cases
   - publisher: Microsoft; pub_date: 2026-05-07 / 2026-05-29; accessed: 2026-08-23; confidence: high; class: audit/controls

11. **Notification nên bám vào record đang sở hữu công việc.** Trong ServiceNow, khi email reply được link vào Case mở, agent được assign Case nhận bell notification và reply xuất hiện trong case activity stream.
   - source: https://www.servicenow.com/docs/r/customer-service-management/email-reply-routing-closed-interactions.html
   - publisher: ServiceNow; pub_date: 2026-05-11; accessed: 2026-08-23; confidence: high; class: notification

## Inferences áp dụng cho phản ánh đô thị

1. **Nên dùng 4 lớp record:** `Submission/Interaction` (mỗi lần gửi/kênh, immutable envelope) → `Case` (hồ sơ giao tiếp với người dân) → `Incident` hoặc `Request` (đơn vị vận hành) → `Task` (việc nội bộ).
   - basis: ServiceNow tách Interaction/Case/Incident/Request/Case Task và Dynamics tách activity/Case/queue item.
   - sources: https://www.servicenow.com/docs/r/customer-service-management/email-reply-routing-closed-interactions.html ; https://www.servicenow.com/docs/r/customer-service-management/csm-item-agent-tasks.html ; https://www.servicenow.com/docs/r/customer-service-management/csm-cases-case-tasks-overview.html ; https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/set-up-queues-manage-activities-cases
   - publisher: ServiceNow/Microsoft; pub_date: 2026-03-12..2026-05-29; accessed: 2026-08-23; confidence: high; class: architecture-inference

2. **Cardinality đề xuất:** nhiều Submission → một Case; nhiều Contact ↔ một Case qua participant/related-party table; nhiều Case → một Incident đô thị; một Case/Incident → nhiều Task. “Incident has Customer Cases related list” và Case có related parties là bằng chứng gần nhất; tài liệu không công bố DDL cardinality đầy đủ.
   - sources: https://www.servicenow.com/docs/r/customer-service-management/csm-item-agent-tasks.html ; https://www.servicenow.com/docs/r/customer-service-management/csm-cases-case-tasks-overview.html
   - publisher: ServiceNow; pub_date: 2026-03-12; accessed: 2026-08-23; confidence: medium; class: relationship-inference

3. **Duplicate handling nên có hai tầng:** (a) ingest idempotency/thread keys để gắn submission vào record hiện hữu; (b) human-confirmed merge với master, lineage/redirect và policy reparent rõ ràng cho activity/attachment/child. Không nên coi similarity score tự động là merge.
   - sources: https://www.servicenow.com/docs/r/customer-service-management/email-reply-routing-closed-interactions.html ; https://learn.microsoft.com/en-us/dynamics365/customer-service/use/customer-service-hub-user-guide-merge-cases ; https://help.salesforce.com/s/articleView?id=sf.cases_merge.htm&language=en_US&type=5
   - publisher: ServiceNow/Microsoft/Salesforce; pub_date: 2026-05-11 / 2026-05-08 / n.d.; accessed: 2026-08-23; confidence: high; class: design-inference

4. **Ownership nên phân lớp:** Case owner chịu giao tiếp, SLA phản hồi và đóng Case; Incident owner chịu khắc phục vận hành; Task assignee chịu hành động cụ thể. Resolution của Incident không tự động đồng nghĩa Case đã đóng—Case chỉ đóng sau khi cập nhật/communicate outcome theo policy.
   - sources: https://www.servicenow.com/docs/r/customer-service-management/csm-cases-case-tasks-overview.html ; https://www.servicenow.com/docs/r/customer-service-management/csm-item-agent-tasks.html ; https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/apply-slas?tabs=customerserviceadmincenter
   - publisher: ServiceNow/Microsoft; pub_date: 2025-05-02..2026-03-12; accessed: 2026-08-23; confidence: medium; class: governance-inference

5. **Audit/notification nên là event ledger first-class:** ghi create/link/unlink/merge/state/assignment/SLA/notification với actor, timestamp, before/after và correlation id; không dựa hoàn toàn vào audit mặc định của vendor vì có hành vi membership không được capture.
   - sources: https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/enable-audit-tables ; https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/set-up-queues-manage-activities-cases ; https://www.servicenow.com/docs/r/customer-service-management/email-reply-routing-closed-interactions.html
   - publisher: Microsoft/ServiceNow; pub_date: 2026-05-07..2026-05-29; accessed: 2026-08-23; confidence: high; class: controls-inference

## Giới hạn và mâu thuẫn

- Không tìm thấy trong 8 nguồn một định nghĩa vendor-neutral cho `report`; vì vậy `Report/Submission` ở trên là thuật ngữ miền đề xuất, không phải fact của vendor.
- Dynamics dùng `incident` làm logical name của Case, trái với ServiceNow nơi Incident là record ITSM riêng. Mọi mapping phải dùng semantic role, không map theo tên bảng.
- Merge semantics khác nhau: Dynamics giữ source ở canceled/merged và reparent activity/email/attachment; Salesforce có thể soft-delete hoặc giữ source. Không có một hành vi merge phổ quát.
- Tài liệu ServiceNow chứng minh related list “Customer Cases” trên Incident nhưng không cung cấp DDL cardinality ở trang đã đọc; kết luận nhiều Case → một Incident được đánh confidence medium.
- Không kiểm chứng được trong ngân sách này chi tiết audit-history sau merge của Salesforce, SLA riêng của ServiceNow Incident/Request, hay notification matrix đầy đủ; không nên suy diễn các phần đó.

## Source set (8)

1. Microsoft Learn — Merge cases — 2026-05-08 — https://learn.microsoft.com/en-us/dynamics365/customer-service/use/customer-service-hub-user-guide-merge-cases
2. Microsoft Learn — Apply SLAs — 2025-05-02 — https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/apply-slas?tabs=customerserviceadmincenter
3. Microsoft Learn — Create and manage basic queues for cases — 2026-05-29 — https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/set-up-queues-manage-activities-cases
4. Microsoft Learn — Enable tables for Customer Service audit logs — 2026-05-07 — https://learn.microsoft.com/en-us/dynamics365/customer-service/administer/enable-audit-tables
5. ServiceNow Docs — Cases and case tasks — 2026-03-12 — https://www.servicenow.com/docs/r/customer-service-management/csm-cases-case-tasks-overview.html
6. ServiceNow Docs — Email reply linking for closed interactions — 2026-05-11 — https://www.servicenow.com/docs/r/customer-service-management/email-reply-routing-closed-interactions.html
7. ServiceNow Docs — Create ITSM records from cases — 2026-03-12 — https://www.servicenow.com/docs/r/customer-service-management/csm-item-agent-tasks.html
8. Salesforce Help — Merge Duplicate Cases in Lightning Experience — n.d. — https://help.salesforce.com/s/articleView?id=sf.cases_merge.htm&language=en_US&type=5
