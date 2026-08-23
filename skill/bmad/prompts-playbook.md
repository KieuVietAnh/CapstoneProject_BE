# UrbanService Planning Playbook

## 1. Research

**Khi dùng:** yêu cầu phụ thuộc mô hình ngành, chuẩn hoặc hệ thống bên ngoài.

**Đầu ra:** bằng chứng có nguồn, terminology, alternatives, recommendation và open questions.

## 2. Product brief / PRD

**Khi dùng:** đã hiểu vấn đề nhưng cần chốt phạm vi và hành vi người dùng.

**Đầu ra:** actors, journeys, functional requirements, NFR và acceptance criteria.

**Gate:** phân biệt rõ dữ liệu người dùng gửi với aggregate nghiệp vụ được xử lý.

## 3. Architecture

**Khi dùng:** thay đổi entity, workflow, API hoặc nhiều module.

**Đầu ra:** invariants, entity ownership, state machine, API contracts, migration và risks.

**Gate:** controller/BLL/DAL ownership, authorization, SLA và backward compatibility.

## 4. Spec / Stories

**Khi dùng:** kiến trúc đã được duyệt và cần cắt việc triển khai.

**Mỗi story phải có:** Given/When/Then AC, file map, dependency, test và migration impact.

**Thứ tự ưu tiên:** additive schema → dual-write/backfill → API read path → workflow cutover → cleanup.

## 5. Build

- Đọc `AGENTS.md` và `skill/INDEX.md`.
- Không tự đổi workflow, role, public contract hoặc database production.
- Thêm test theo rủi ro; migration mới không sửa migration cũ.
- Giữ controller mỏng và business rule ở BLL.

## 6. Review

- Review adversarial theo security, data integrity, concurrency, authorization và compatibility.
- Kiểm tra API người dân không lộ dữ liệu user khác.
- Kiểm tra migration backfill và rollback.
- Kiểm tra status/SLA/notification không bị double-run.

## 7. Verify và handoff

```powershell
dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj
dotnet build UrbanService.sln
git diff --check
```

Bàn giao phải nêu file chính, dữ liệu/API, test, việc còn lại và rủi ro.
