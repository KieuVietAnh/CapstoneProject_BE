# Done — Incident Schema Foundation

Ngày hoàn tất code: 2026-08-23.

## Đã làm

- Dùng kết luận từ [`../../research/report-incident-models/`](../../research/report-incident-models/)
  để tách Report khỏi Incident và giữ audit/link history.
- Giữ `Feedback` là Report và thêm aggregate `Incident` độc lập.
- Thêm `Incident`, `IncidentReportLink`, `IncidentEvent`, `IncidentSubscription`.
- Thêm khóa ngoại, check constraint, partial unique index cho một active link/Report,
  unique subscription và index phục vụ queue/timeline.
- Tạo migration `20260823041239_AddIncidentAggregateSchema` cùng model snapshot.
- Apply migration thành công vào PostgreSQL/Supabase được cấu hình ngày 2026-08-23.
- Backfill một Incident cho mỗi canonical feedback root, link toàn bộ feedback con,
  subscribe người gửi và ghi event backfill.
- Giữ API xóa Feedback cũ hoạt động bằng cascade report link; event/subscription không
  mất theo Feedback.

## Đã kiểm chứng

- `dotnet build UrbanService.sln --no-restore`: pass, 0 warning, 0 error.
- `dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj --no-build --no-restore`:
  101/101 pass.
- `dotnet ef migrations has-pending-model-changes`: không có model change chưa scaffold.
- `dotnet ef migrations list`: migration Incident là migration mới nhất và đã apply.

## Ngoài phạm vi lát cắt schema

- Incident intake/read/link P0 được ghi riêng tại [`incident-api-p0.md`](incident-api-p0.md).
- Workflow status/SLA/assignment/resolution chưa cutover khỏi Feedback; tiếp tục được
  theo dõi tại `../platform-backlog.md` và `../incident-api-plan.md`.
