---
title: 'Admin xóa phản ánh'
type: 'feature'
created: '2026-08-19'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'e35a23e3d907ee4c2192cf74d039525062cba248'
baseline_commits:
  backend: 'e35a23e3d907ee4c2192cf74d039525062cba248'
  frontend: '1325ebad9f7a8ceb2d5312de70961f12e0c7e5e6'
context: ['BE/UrbanService/AGENTS.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Admin xem được toàn bộ phản ánh nhưng chưa có API hoặc thao tác FE để xóa. Endpoint của người dân yêu cầu ownership nên không phù hợp.

**Approach:** Thêm hard-delete tại management API, chỉ cho `SYSTEMADMIN`; xử lý FK ứng viên trùng lặp và cung cấp nút xóa có xác nhận tại danh sách admin.

## Boundaries & Constraints

**Always:** Dùng `DELETE /api/management/feedbacks/{feedbackId:guid}`, trả `204 No Content`; backend phải có action-level `SYSTEMADMIN`. Trước khi xóa `Feedback`, service xóa các `FeedbackDuplicateCandidate` tham chiếu target qua `PotentialParentFeedbackId` trong cùng `SaveAsync`; các quan hệ Cascade/SetNull giữ nguyên. FE phải cảnh báo xóa vĩnh viễn, chặn double-submit/row navigation, hiển thị lỗi để retry, evict cache và reconcile list sau thành công.

**Ask First:** Dừng nếu cần soft-delete, migration, đổi ánh xạ lỗi toàn cục, cleanup Cloudinary, hoặc mở quyền cho role khác.

**Never:** Không gọi `/api/user/feedbacks`, không chỉ dựa vào FE guard, không đổi workflow/status, không chạy destructive E2E trên dữ liệu dùng chung.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Xóa thành công | Admin, feedback tồn tại | Candidate-parent reference bị dọn; feedback/dependent bị hard-delete; child `ParentTicketId` được SetNull; API trả 204; FE bỏ row/cache và refresh nền | FE không phụ thuộc response body |
| ID không tồn tại | GUID hợp lệ | Không Delete/Save | Trả 400 theo middleware hiện tại; modal giữ mở và hiện message |
| Sai role | Staff/manager/citizen/anonymous | Service không chạy | Authorization trả 403/401 |
| Gửi lặp hoặc refresh race | Xác nhận nhiều lần/prefetch đang chạy | Chỉ một DELETE; cache cũ không ghi lại record | Disable/loading; lỗi cho phép retry |

</frozen-after-approval>

## Code Map

- `BE/UrbanService/UrbanService/Controllers/ManagementFeedbacksController.cs:71`, `UrbanService.BLL/Interfaces/IFeedbackService.cs:33` -- DELETE admin-only/204 và contract management đã có; giữ nguyên.
- `BE/UrbanService/UrbanService.BLL/Services/FeedbackService.cs:871`, `UrbanService.DAL/Data/UrbanServiceDbContext.cs:407` -- thêm cleanup candidate-parent `Restrict`; giữ Cascade/SetNull, không migration.
- `BE/UrbanService/UrbanService.BLL.Tests/FeedbackAdminDeleteTests.cs` -- test service và metadata controller.
- `FE/UrbanService-FE/packages/shared-api/src/managementFeedbackApi.js:433`, `managementFeedbackApi.test.js` -- client đúng route đã có; bổ sung test 204/ID rỗng.
- `FE/UrbanService-FE/apps/web/src/pages/staff/ManagementFeedbackListPage.jsx:167` -- gỡ state/handler/nút xóa tại dòng 687/1099.
- `FE/UrbanService-FE/apps/web/src/services/cache/adminFeedbackDetailCache.js:21`, `pages/management/FeedbackManagement.jsx:303`, `pages/management/FeedbackDetailPage.jsx:175` -- invalidation chống stale prefetch/router history và UX delete admin.
- `FE/UrbanService-FE/package.json`, `.github/workflows/playwright.yml` -- đưa các test delete/cache/reconcile vào test gate chuẩn.
- `FE/UrbanService-FE/apps/web/src/routes/AppRoutes.jsx:323` -- chỉ đọc: staff chỉ `SYSTEM_STAFF`; admin tại dòng 545 chỉ `ADMINISTRATOR`.

## Tasks & Acceptance

**Execution:**
- [x] `BE/UrbanService/UrbanService.BLL/Services/FeedbackService.cs`, `BE/UrbanService/UrbanService.BLL.Tests/FeedbackAdminDeleteTests.cs` -- cleanup FK; test success/candidate/not-found và admin-only/204.
- [x] `FE/UrbanService-FE/packages/shared-api/src/managementFeedbackApi.test.js` -- khóa contract DELETE và 204/ID rỗng.
- [x] `FE/UrbanService-FE/apps/web/src/pages/staff/ManagementFeedbackListPage.jsx` -- gỡ toàn bộ delete khỏi staff.
- [x] `FE/UrbanService-FE/apps/web/src/services/cache/adminFeedbackDetailCache.js`, `FE/UrbanService-FE/apps/web/src/pages/management/FeedbackManagement.jsx`, `FE/UrbanService-FE/apps/web/src/pages/management/FeedbackDetailPage.jsx` -- modal admin, retry/double-submit/focus guard và reconcile cache/list/metric/history.
- [x] `FE/UrbanService-FE/package.json`, `FE/UrbanService-FE/.github/workflows/playwright.yml` -- chạy helper tests và DELETE contract trong CI.

**Acceptance Criteria:**
- Given staff mở `/staff/feedbacks`, when xem từng hàng, then không có nút hoặc handler xóa.
- Given admin xác nhận xóa feedback tồn tại, when DELETE hoàn tất, then nhận 204, row/metric/cache được cập nhật và có thông báo thành công.
- Given caller không phải `SYSTEMADMIN`, when gọi endpoint, then nhận 401/403 và service không chạy.
- Given delete lỗi hoặc prefetch cũ hoàn tất muộn, when admin retry/mở detail, then record lỗi còn nguyên và record đã xóa không hồi sinh từ cache.

## Spec Change Log

## Design Notes

Candidate-parent là FK `Restrict` duy nhất cần dọn tường minh. Delete chỉ load bản ghi gốc, để database xử lý Cascade/SetNull, tránh Cartesian include không cần thiết. Modal dùng error state riêng vì error chung thay toàn bộ bảng; focus được giữ trong dialog và trả về trigger khi hủy. Cache chuẩn hóa GUID không phân biệt hoa/thường; detail 400/404 loại bỏ router-state cũ. BE hiện ở `feature/messenger-evidence-images`; phải merge/deploy cùng FE để production có endpoint.

## Verification

**Commands:**
- BE: `dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj`; `dotnet build UrbanService.sln` -- pass.
- FE: `pnpm test:admin-feedback-delete`; `pnpm test:admin-feedback-delete-api`; `pnpm --dir apps/web lint`; `pnpm --dir apps/web build` -- pass (lint/build chỉ còn warning có sẵn ngoài story).
- Known pre-existing: chạy toàn bộ `node --test packages/shared-api/src/managementFeedbackApi.test.js` có 7/11 pass; bốn assertion normalize/provider cũ đã ghi vào `deferred-work.md`, còn test DELETE mới pass qua command riêng ở trên.
- Cả hai repo: `git diff --check` -- không lỗi.

**Manual checks:**
- Staff không thấy delete; admin modal không double-submit, lỗi retry được, success loại record khỏi list/cache.

## Suggested Review Order

**Luồng xóa admin**

- Điểm vào điều phối DELETE, chống gửi lặp và reconcile giao diện.
  [`FeedbackManagement.jsx:685`](../../../../FE/UrbanService-FE/apps/web/src/pages/management/FeedbackManagement.jsx#L685)

- Nút nguy hiểm chỉ xuất hiện trên bảng quản trị admin.
  [`FeedbackManagement.jsx:1019`](../../../../FE/UrbanService-FE/apps/web/src/pages/management/FeedbackManagement.jsx#L1019)

- Trang staff chỉ còn hành động xem chi tiết.
  [`ManagementFeedbackListPage.jsx:1052`](../../../../FE/UrbanService-FE/apps/web/src/pages/staff/ManagementFeedbackListPage.jsx#L1052)

**An toàn dữ liệu backend**

- Xóa mọi candidate-parent trước feedback trong một lần lưu.
  [`FeedbackService.cs:871`](../../UrbanService.BLL/Services/FeedbackService.cs#L871)

- Khóa success, missing ID, thứ tự lưu, role và quan hệ FK.
  [`FeedbackAdminDeleteTests.cs:41`](../../UrbanService.BLL.Tests/FeedbackAdminDeleteTests.cs#L41)

**Nhất quán cache và màn hình**

- Helper chỉ mutate list/cache sau khi DELETE thành công.
  [`adminFeedbackDeletion.js:24`](../../../../FE/UrbanService-FE/apps/web/src/services/adminFeedbackDeletion.js#L24)

- Cache chuẩn hóa GUID và chặn prefetch cũ ghi ngược.
  [`adminFeedbackDetailCache.js:16`](../../../../FE/UrbanService-FE/apps/web/src/services/cache/adminFeedbackDetailCache.js#L16)

- Detail loại router-state cũ khi backend báo không tồn tại.
  [`FeedbackDetailPage.jsx:176`](../../../../FE/UrbanService-FE/apps/web/src/pages/management/FeedbackDetailPage.jsx#L176)

- Reconcile giảm đúng tổng và nhóm trạng thái.
  [`adminFeedbackMetrics.js:81`](../../../../FE/UrbanService-FE/apps/web/src/utils/adminFeedbackMetrics.js#L81)

**Contract và quality gate**

- Test khóa method, route, trim ID và response 204 rỗng.
  [`managementFeedbackApi.test.js:196`](../../../../FE/UrbanService-FE/packages/shared-api/src/managementFeedbackApi.test.js#L196)

- Script riêng chạy toàn bộ test của feature không phụ thuộc mạng.
  [`package.json:21`](../../../../FE/UrbanService-FE/package.json#L21)

- CI chạy helper behavior và API contract trước lint/build.
  [`playwright.yml:57`](../../../../FE/UrbanService-FE/.github/workflows/playwright.yml#L57)
