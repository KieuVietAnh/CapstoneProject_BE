---
title: 'Gửi phản ánh từ dữ liệu AI đã thu thập mà không gọi AI lần cuối'
type: 'bugfix'
created: '2026-08-18'
status: 'done'
review_loop_iteration: 0
baseline_commit: '0c8f3609d03f5b9097009eef9da633145d15e184'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Luồng tạo phản ánh trong Citizen AI Copilot gọi `/api/ai/feedback-draft` sau khi đã thu thập đủ tiêu đề, mô tả, vị trí và ảnh. Với nội dung dài, prompt vượt context 4.096 token và làm người dân không thể gửi phản ánh dù dữ liệu cần thiết đã có sẵn.

**Approach:** Coi các câu trả lời trong luồng tạo phản ánh là bản nháp nguồn sự thật, tự lưu bản nháp khi người dùng cung cấp thông tin và để nút xác nhận gọi trực tiếp API tạo feedback. AI chat vẫn được dùng cho các câu hỏi hỗ trợ thông thường, nhưng không nằm trên đường gửi cuối cùng.

## Boundaries & Constraints

**Always:** Giữ dữ liệu người dùng nhập nguyên văn; giữ ảnh trong state để upload multipart; dùng API area/category hiện có để bổ sung ID bắt buộc; nếu chưa đủ ID bắt buộc thì chuyển sang trang tạo phản ánh với dữ liệu đã điền thay vì gọi AI; khóa nút trong lúc gửi để tránh tạo trùng; xóa bản nháp sau khi tạo thành công.

**Ask First:** Nếu cần đổi contract API tạo feedback, thêm bảng/migration hoặc thay đổi quy tắc bắt buộc area/category thì phải dừng và hỏi người dùng.

**Never:** Không gửi toàn bộ hội thoại, ảnh base64 hoặc bản nháp dài tới `/api/ai/feedback-draft` khi người dùng bấm tạo; không cắt bớt mô tả trước khi gọi API tạo feedback; không âm thầm bỏ ảnh đã chọn.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Đủ thông tin | Tiêu đề, mô tả, vị trí và area/category phân giải được | Gọi thẳng `POST /api/user/feedbacks` kèm ảnh, không gọi `/api/ai/feedback-draft` | Hiển thị lỗi BE trong chat và giữ nguyên bản nháp |
| Thiếu ID bắt buộc | Không phân giải được area hoặc category | Mở `/tickets/create` với dữ liệu đã thu thập để người dùng bổ sung | Không gọi AI và không làm mất nội dung |
| Nội dung dài | Mô tả dài hơn giới hạn context AI | Vẫn gửi nguyên văn qua API feedback | Chỉ áp dụng giới hạn upload/validation của API feedback |
| Tải lại trước khi gửi | Đã nhập một phần dữ liệu | Khôi phục bản nháp văn bản/vị trí đã lưu; yêu cầu chọn lại file nếu cần | Bản nháp lỗi định dạng được bỏ qua an toàn |

</frozen-after-approval>

## Code Map

- `FE/UrbanService-FE/apps/web/src/components/public/CitizenAiCopilot.jsx` -- chứa state và state machine thu thập title/description/location/evidence; `handleCreateDraft` hiện gọi AI rồi mới gọi `ticketApi.createTicket`; đây là điểm thay đổi chính.
- `FE/UrbanService-FE/apps/web/src/pages/tickets/CreateTicketPage.jsx` -- mẫu lưu bản nháp theo user trong `localStorage` và điểm nhận `routeLocation.state.aiDraft`; tái sử dụng schema/khóa bản nháp và cơ chế fallback sang form.
- `FE/UrbanService-FE/packages/shared-api/src/ticketApi.js` -- `createTicket` đã đóng gói multipart và upload attachments đến endpoint Service User; giữ nguyên contract.
- `FE/UrbanService-FE/packages/shared-api/src/toolsApi.js` -- `createAiFeedbackDraft` là call cần loại khỏi đường tạo cuối; các call lấy area/category vẫn được dùng.
- `BE/UrbanService/UrbanService/Controllers/UserFeedbacksController.cs` -- endpoint `POST /api/user/feedbacks` nhận form-data; chỉ dùng làm contract, không cần sửa.
- `BE/UrbanService/UrbanService.BLL/Services/AiFeedbackDraftService.cs` -- đang có thay đổi cục bộ giảm prompt/base64; giữ nguyên công việc hiện có, không phụ thuộc vào service này để gửi phản ánh.

## Tasks & Acceptance

**Execution:**
- [x] `FE/UrbanService-FE/apps/web/src/components/public/CitizenAiCopilot.jsx` -- tự lưu/khôi phục dữ liệu draft trong quá trình state machine cập nhật; thay thao tác cuối bằng tạo ticket trực tiếp hoặc fallback sang form; cập nhật nhãn và thông báo theo đúng hành vi.
- [x] `FE/UrbanService-FE/apps/web/src/components/public/CitizenAiCopilot.jsx` -- bảo toàn retry, chống double-submit, ảnh và lỗi API trong mọi nhánh của ma trận.

**Acceptance Criteria:**
- Given người dùng đã hoàn tất các câu hỏi tạo phản ánh, when bấm nút gửi, then không có request đến `/api/ai/feedback-draft` và feedback được gửi thẳng tới backend từ bản nháp.
- Given backend tạo feedback trả lỗi, when thao tác kết thúc, then nội dung và ảnh vẫn còn để người dùng thử lại.
- Given tạo feedback thành công, when backend trả kết quả, then UI báo thành công và xóa draft đã lưu.

## Spec Change Log

## Design Notes

State trong Copilot là bản nháp chạy trực tiếp. `localStorage` chỉ lưu dữ liệu tuần tự hóa được (không lưu nội dung file); file vẫn nằm trong component state và sau reload UI phải nói rõ cần chọn lại ảnh. Endpoint AI draft có thể tiếp tục tồn tại cho các luồng khác nhưng không được gọi bởi nút gửi của Copilot.

## Verification

**Commands:**
- `pnpm --dir apps/web lint` -- expected: không có lỗi lint mới trong component đã sửa.
- `pnpm --dir apps/web build` -- expected: Vite build thành công.
- `git diff --check` trong FE và BE -- expected: không có lỗi whitespace.

**Manual checks:**
- Kiểm tra Network khi tạo phản ánh dài: có `POST /api/user/feedbacks`, không có `POST /api/ai/feedback-draft`.
- Kiểm tra ảnh được gửi multipart, lỗi BE giữ dữ liệu, và reload khôi phục draft văn bản/vị trí.

## Suggested Review Order

**Luồng gửi trực tiếp**

- Điểm vào loại bỏ AI cuối và chọn gửi thẳng hoặc hoàn tất trong form.
  [`CitizenAiCopilot.jsx:639`](../../../../FE/UrbanService-FE/apps/web/src/components/public/CitizenAiCopilot.jsx#L639)

- Submission plan bảo toàn nội dung dài, tọa độ và file multipart.
  [`citizenAiFeedbackDraft.js:25`](../../../../FE/UrbanService-FE/apps/web/src/components/public/citizenAiFeedbackDraft.js#L25)

- Area/category chỉ được dùng khi khớp dữ liệu backend, tránh gán mặc định sai.
  [`CitizenAiCopilot.jsx:408`](../../../../FE/UrbanService-FE/apps/web/src/components/public/CitizenAiCopilot.jsx#L408)

**Bản nháp và fallback**

- Draft được tách theo tài khoản, khôi phục an toàn và không làm mất mô tả dài.
  [`CitizenAiCopilot.jsx:247`](../../../../FE/UrbanService-FE/apps/web/src/components/public/CitizenAiCopilot.jsx#L247)

- File được chuyển sang form khi thiếu ID bắt buộc.
  [`CreateTicketPage.jsx:267`](../../../../FE/UrbanService-FE/apps/web/src/pages/tickets/CreateTicketPage.jsx#L267)

- Gửi thành công từ form xóa luôn draft nguồn của Copilot.
  [`CreateTicketPage.jsx:938`](../../../../FE/UrbanService-FE/apps/web/src/pages/tickets/CreateTicketPage.jsx#L938)

**Kiểm thử**

- Kiểm tra không còn AI call, nội dung dài, fallback và draft hỏng.
  [`citizenAiFeedbackDraft.test.js:9`](../../../../FE/UrbanService-FE/apps/web/src/components/public/citizenAiFeedbackDraft.test.js#L9)
