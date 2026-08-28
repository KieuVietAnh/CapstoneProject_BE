---
title: 'Bổ sung API quên mật khẩu bằng OTP email'
type: 'feature'
created: '2026-08-16'
status: 'in-progress'
baseline_commit: '5079be4251cfd9165b625bf43267972ae54fb143'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** UrbanService chưa có API tự phục vụ khi người dùng quên mật khẩu; DTO và contract nháp có sẵn nhưng chưa được triển khai.

**Approach:** Thêm luồng công khai hai bước: gửi OTP sáu chữ số qua email, sau đó dùng email + OTP để đặt mật khẩu mới. Tái sử dụng Brevo, `PasswordHasher`, `IMemoryCache` và cơ chế thu hồi refresh token hiện tại.

## Boundaries & Constraints

**Always:** Controller mỏng, rule trong `AuthService`; chuẩn hóa email; OTP dùng CSPRNG, chỉ lưu hash, sống 5 phút, cooldown 60 giây, tối đa 5 lần sai, gắn đúng email/mục đích và chỉ dùng một lần; mật khẩu mới tối thiểu 6 ký tự; reset thành công hash mật khẩu, xóa/revoke refresh token và cập nhật UTC; chuyển `CancellationToken` tới email I/O; không log OTP, mật khẩu, hash, email hay secret.

**Ask First:** Thêm Redis/bảng token/migration; đổi password policy; đổi route/response công khai; bổ sung thu hồi access JWT còn hạn.

**Never:** Tiết lộ email tồn tại, bị khóa hay đang cooldown qua response; lưu OTP plaintext; dùng template `OTPEmail.html` mang thương hiệu Luna Bloom; gửi email thật trong test; apply migration production.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Gửi OTP | Email active | `POST /api/auth/forgot-password/send-otp` trả 204, gửi email, cache hash OTP | Không trả OTP |
| Chống dò tài khoản | Email không tồn tại/inactive/cooldown | Cùng 204 rỗng; không gửi email dư | Không nêu lý do |
| Input email lỗi | Email trống/sai định dạng | Không tạo OTP/email | 400 validation chung |
| Brevo lỗi | Gửi email thất bại | Không giữ OTP; response vẫn không phân biệt tài khoản | Log không PII/secret |
| Reset thành công | Email + OTP còn hạn + password hợp lệ | `POST /api/auth/forgot-password/reset` trả 204, đổi hash, revoke refresh, consume OTP | OTP không dùng lại |
| Reset thất bại | Email sai/khóa; OTP sai/hết hạn/khác email/quá 5 lần; password yếu | Không đổi user; password yếu không consume OTP; lần sai thứ 5 hủy OTP | Một lỗi 400 chung theo nhóm lỗi |

</frozen-after-approval>

## Code Map

- `UrbanService/Controllers/AuthController.cs:10-118` -- thêm hai endpoint anonymous, 204 và Swagger.
- `UrbanService.BLL/DTOs/AuthDto.cs:50-65`, `UrbanService.BLL/Interfaces/IAuthService.cs:5-22` -- bỏ `Username` lỗi thời; hoàn thiện request/contract async.
- `UrbanService.BLL/Services/AuthService.cs:18-24,206-269,366-369` -- reuse mẫu OTP; thêm state riêng, cooldown, attempt limit, anti-enumeration và reset.
- `UrbanService.BLL/Common/Securities/PasswordHasher.cs:5-27`, `UrbanService.BLL/Interfaces/IEmailSender.cs:5-8` -- reuse PBKDF2 và Brevo; mock email trong test.
- `UrbanService.DAL/Entities/User.cs:14-34`, `UrbanService.DAL/Data/UrbanServiceDbContext.cs:111-136` -- password/refresh và email unique; read-only, không migration.
- `UrbanService.BLL.Tests/AuthServiceTests.cs` -- unit test mới dùng xUnit, NSubstitute, MemoryCache và async-query helper.

## Tasks & Acceptance

**Execution:**
- [ ] `UrbanService.BLL/DTOs/AuthDto.cs`, `UrbanService.BLL/Interfaces/IAuthService.cs`, `UrbanService/Controllers/AuthController.cs` -- hoàn thiện contract và expose hai POST public trả 204.
- [ ] `UrbanService.BLL/Services/AuthService.cs` -- triển khai OTP, anti-enumeration, giới hạn thử, reset và thu hồi refresh.
- [ ] `UrbanService.BLL.Tests/AuthServiceTests.cs` -- bao phủ toàn bộ ma trận, lỗi email/save và one-time/concurrency.

**Acceptance Criteria:**
- Given cùng OTP bị dùng lại hoặc đồng thời, when reset, then không có lần đổi mật khẩu thứ hai.
- Given chạy test/build và xem diff, when bàn giao, then không có email thật, secret, PII, build artifact hay thay đổi route auth cũ.

## Spec Change Log

## Design Notes

`IMemoryCache` giữ scope tương thích với OTP hiện tại và không cần schema/config mới. OTP sẽ mất khi restart và không chia sẻ giữa nhiều replica; nếu production cần multi-instance/durable reset thì phải xin chuyển sang Redis/database. Reset chỉ revoke refresh token; access JWT đã phát vẫn hợp lệ đến khi hết hạn.

## Verification

**Commands:**
- `dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj` -- toàn bộ unit test pass.
- `dotnet build UrbanService.sln` -- solution build không có error mới.
- `git diff --check` -- diff sạch, không chứa secret hoặc thay đổi ngoài scope.
