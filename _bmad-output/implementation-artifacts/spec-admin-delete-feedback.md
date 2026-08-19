---
title: 'Admin xóa phản ánh'
type: 'feature'
created: '2026-08-19'
status: 'draft'
review_loop_iteration: 0
baseline_commits:
  backend: 'c921a1ed62f04d91a98eeb4d643fcaa386e54b5f'
  frontend: '6b91918f615782498360bec669468dd57bad22b8'
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

- `BE/UrbanService/UrbanService/Controllers/ManagementFeedbacksController.cs` -- thêm action DELETE, override class authorization bằng `SYSTEMADMIN`, trả `NoContent()`.
- `BE/UrbanService/UrbanService.BLL/Interfaces/IFeedbackService.cs` -- thêm `DeleteByAdminAsync(Guid feedbackId)` tách khỏi owner delete.
- `BE/UrbanService/UrbanService.BLL/Services/FeedbackService.cs` -- query không theo owner, dọn candidate-parent, delete và lưu một lần.
- `BE/UrbanService/UrbanService.DAL/Data/UrbanServiceDbContext.cs` -- read-only evidence: candidate-parent `Restrict`; dependent khác Cascade/SetNull; không migration.
- `BE/UrbanService/UrbanService.BLL.Tests/FeedbackAdminDeleteTests.cs` -- service success/candidate/not-found và controller route/role/action tests.
- `FE/UrbanService-FE/packages/shared-api/src/managementFeedbackApi.js` -- thêm management DELETE; 204 được unwrap thành `undefined`.
- `FE/UrbanService-FE/packages/shared-api/src/managementFeedbackApi.test.js` -- stub axios và assert endpoint/result.
- `FE/UrbanService-FE/apps/web/src/services/cache/adminFeedbackDetailCache.js` -- invalidator ngăn in-flight prefetch ghi lại cache.
- `FE/UrbanService-FE/apps/web/src/pages/management/FeedbackManagement.jsx` -- row action, modal, loading/error/toast, cập nhật row/metric và refresh nền.
- `FE/UrbanService-FE/apps/web/src/routes/AppRoutes.jsx` -- read-only evidence: management routes đã administrator-only.

## Tasks & Acceptance

**Execution:**
- [ ] `UrbanService/Controllers/ManagementFeedbacksController.cs`, `UrbanService.BLL/Interfaces/IFeedbackService.cs`, `UrbanService.BLL/Services/FeedbackService.cs` -- thêm endpoint/service admin và xử lý FK candidate; không đổi schema.
- [ ] `UrbanService.BLL.Tests/FeedbackAdminDeleteTests.cs` -- test delete, not-found và admin-only metadata.
- [ ] `packages/shared-api/src/managementFeedbackApi.js`, `packages/shared-api/src/managementFeedbackApi.test.js` -- nối và test management DELETE.
- [ ] `apps/web/src/services/cache/adminFeedbackDetailCache.js`, `apps/web/src/pages/management/FeedbackManagement.jsx` -- hoàn thiện invalidation và UX xác nhận/retry/reconcile.

**Acceptance Criteria:**
- Given admin xác nhận xóa feedback tồn tại, when DELETE hoàn tất, then nhận 204, row/metric/cache được cập nhật và có thông báo thành công.
- Given caller không phải `SYSTEMADMIN`, when gọi endpoint, then nhận 401/403 và không có dữ liệu bị xóa.
- Given backend từ chối delete, when request lỗi, then record còn nguyên, modal hiện message và cho retry.
- Given prefetch cũ hoàn tất sau delete, when admin mở lại detail, then FE không hiển thị record stale.

## Spec Change Log

## Design Notes

Hard-delete giữ parity với citizen delete. FK candidate-parent là ngoại lệ `Restrict` cần dọn tường minh; DB xử lý phần còn lại. Notification cũ và Cloudinary asset có thể còn link/URL chết vì không có FK/PublicId phù hợp, nên nằm ngoài scope.

## Verification

- `dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj` và `dotnet build UrbanService.sln` tại BE -- pass.
- `node --test packages/shared-api/src/managementFeedbackApi.test.js`, `pnpm --dir apps/web lint`, `pnpm --dir apps/web build` tại FE -- pass.
- `git diff --check` trong cả hai repo -- không lỗi whitespace.
- Manual: admin nhận 204 body rỗng; non-admin nhận 401/403; modal không double-submit và giữ record khi lỗi.
