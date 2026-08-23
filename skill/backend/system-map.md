# UrbanService Backend — System Map

## Solution

| Project | Ownership |
|---|---|
| `UrbanService/` | Controller, middleware, filter, SignalR hub, background worker và DI |
| `UrbanService.BLL/` | DTO, interface, constraint, queue và business service |
| `UrbanService.DAL/` | Entity, DbContext, repository, unit of work và EF migration |
| `UrbanService.BLL.Tests/` | Unit test business logic và worker orchestration |

## Công nghệ

- Target framework: .NET 9.
- EF Core/Npgsql: nhánh 8.x hiện có trong project.
- Database: PostgreSQL.
- API docs: Swagger/OpenAPI.
- Realtime: SignalR `/hubs/notifications`.
- Test: xUnit + EF test context hiện có.

## Role

- `SERVICEUSER`: gửi, theo dõi, bình luận, support và review phản ánh thuộc quyền.
- `SYSTEMSTAFF`: xác minh, cập nhật workflow, xử lý duplicate và vận hành khu vực.
- `SYSTEMADMIN`: quản trị tài khoản, cấu hình và dữ liệu toàn hệ thống.
- `INTERACTIONMANAGER`: quản lý tương tác, provider và một phần nghiệp vụ vận hành.
- `SERVICEOPERATORSTAFF`: xử lý công việc phía đơn vị cung cấp dịch vụ.

## Module chính

- Feedback: tiếp nhận, danh sách, chi tiết, comment, support, attachment và notification.
- Duplicate: AI candidate, staff confirm/reject và master/child linking.
- AI: chat, draft, analysis, duplicate classification và knowledge source.
- SLA: policy, lifecycle, event, dashboard, warning và background monitoring.
- Provider: service provider, provider report, contact log, resolution và review.
- Area: operating area, staff assignment, alert, subscription và hotspot.
- Integration: Messenger, Cloudinary, Brevo và Google Auth. Code Zalo vẫn tồn tại nhưng
  `Zalo:Enabled` mặc định `false`; chỉ Messenger đang được giữ hoạt động theo quyết định hiện tại.
- Incident: dual-write Report, staff queue/detail, report link/unlink, subscriptions và audit events.

## API boundary

- Route người dân nằm chủ yếu dưới `/api/user/*` và yêu cầu `SERVICEUSER`.
- Route quản lý nằm dưới `/api/management/*` hoặc controller staff chuyên biệt.
- Controller lấy user ID/role ở biên rồi gọi BLL service.
- Response công khai dùng DTO và wrapper/filter hiện có.

## Persistence

- `UrbanServiceDbContext` là mapping nguồn sự thật của EF.
- Migration mới phải đi kèm model snapshot.
- PostgreSQL constraint/index/trigger đang bảo vệ invariant duplicate master.
- Migration dữ liệu phải có backfill xác định và `Down` hợp lý.

## Verification

```powershell
dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj
dotnet build UrbanService.sln
git diff --check
```
