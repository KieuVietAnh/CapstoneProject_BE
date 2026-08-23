# UrbanService — Skill Index

Nguồn handoff ngắn gọn cho AI agent làm việc với UrbanService Backend. Luôn đọc
`AGENTS.md` trước; file này bổ sung bản đồ hệ thống và các invariant nghiệp vụ.

## Hệ thống

- ASP.NET Core Web API trên .NET 9.
- Entity Framework Core 8 + PostgreSQL.
- Kiến trúc `API → BLL → DAL`; controller mỏng, nghiệp vụ ở BLL, entity/migration ở DAL.
- JWT/RBAC, SignalR, Swagger, xUnit và Docker.
- Kênh tiếp nhận phản ánh đang vận hành: Web và Messenger. Zalo được giữ trong code
  nhưng bị tắt mặc định bằng `Zalo:Enabled=false`.
- Tích hợp tùy chọn: Cloudinary, Brevo, Google Auth, OpenRouter/Gemini, Meta và Zalo OA.

## Development Protocol

1. Đọc `git status`, `AGENTS.md`, contract và test liên quan.
2. Xác định ảnh hưởng lên quyền, workflow, SLA, dữ liệu, API và tích hợp.
3. Giữ thay đổi nhỏ nhất, đúng lớp kiến trúc hiện có.
4. Thêm migration mới cho schema change; không sửa migration đã phát hành.
5. Chạy test liên quan, build solution và `git diff --check`.
6. Báo rõ migration chưa apply, kiểm thử chưa chạy và rủi ro còn lại.

## Hard Rules

- **CRITICAL — Quyền sở hữu:** endpoint người dân phải giới hạn dữ liệu theo user từ JWT.
- **CRITICAL — Phân quyền:** không đổi role, claim hoặc authorization nếu chưa được duyệt.
- **CRITICAL — Workflow:** không tự đổi trạng thái feedback, SLA hoặc duplicate linking.
- **CRITICAL — Dữ liệu:** không xóa migration, lịch sử, attachment hoặc dữ liệu production.
- **CRITICAL — API:** giữ tương thích route/DTO/value công khai; breaking change phải xin duyệt.
- Controller không chứa truy vấn DB hoặc business workflow dài.
- I/O dùng async; truyền `CancellationToken` khi contract hỗ trợ.
- Truy vấn LINQ phải chạy được ở database; không tải toàn bảng để lọc trong RAM.
- API trả DTO, không trả trực tiếp EF entity.
- Thời gian nghiệp vụ dùng `DateTime.UtcNow`.
- Dùng constant hiện có cho role, trạng thái, kênh và loại notification.
- Không log secret, token, nội dung nhạy cảm hoặc stack trace ra client.
- Webhook phải xác thực chữ ký, phản hồi nhanh qua inbox/queue/worker và chống xử lý lặp.
- Không chạy `database update` nếu chưa xác định rõ database được phép thay đổi.

## Baseline nghiệp vụ phản ánh hiện tại

- `Feedback` đang vừa là phản ánh người dân vừa là đơn vị workflow xử lý.
- Feedback mới có `Submitted`, `IsMasterTicket = false`, `ParentTicketId = null`.
- AI review category/priority và duplicate classification chạy độc lập.
- Duplicate detection chỉ tạo candidate; staff quyết định `Pending → Confirmed|Rejected`.
- Candidate được xác nhận liên kết feedback con vào một feedback master cũ hơn, cùng khu vực.
- Feedback con đã liên kết không được chạy workflow xử lý riêng.
- Workflow hiện hữu: `Submitted → AiReviewed/Verified → Assigned → InProgress → SubmittedForApproval → Approved → Closed`, kèm các nhánh `Resolved`, `Rejected`, `NeedRework`, `Cancelled` theo nghiệp vụ hiện tại.
- `Submitted` và `AiReviewed` là trạng thái nội bộ; master có feedback con phải giữ trạng thái công khai hợp lệ.
- Schema `Incident` độc lập đã được apply vào database cấu hình ngày 2026-08-23.
- Luồng tạo Report đã dual-write sang Incident; API staff list/detail/link/unlink đã có.
- Workflow status/SLA/assignment vẫn dùng `Feedback`; `IsMasterTicket`/`ParentTicketId`
  tiếp tục được giữ trong phase tương thích.

## Tài liệu

- [`backend/INDEX.md`](backend/INDEX.md) — bản đồ code và workflow backend.
- [`bmad/INDEX.md`](bmad/INDEX.md) — quy trình lập kế hoạch cho thay đổi lớn.
- [`research/INDEX.md`](research/INDEX.md) — nghiên cứu thực tế và nguồn tham khảo đã hoàn tất.
- [`HOW_TO_UPDATE.md`](HOW_TO_UPDATE.md) — quy tắc cập nhật thư mục này.
