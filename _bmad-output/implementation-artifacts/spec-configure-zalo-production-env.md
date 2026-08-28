---
title: 'Đưa cấu hình Zalo vào môi trường production trên VPS'
type: 'chore'
created: '2026-08-16'
status: 'draft'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `docker-compose.prod.yml` đã nhận đầy đủ biến `ZALO_*`, nhưng workflow deploy không ghi các biến này vào `.env` trên VPS. Vì `.env` bị tạo lại mỗi lần deploy, Zalo webhook và cơ chế làm mới token sẽ nhận cấu hình rỗng dù các giá trị đã được khai báo trong GitHub Environment secrets.

**Approach:** Bổ sung ánh xạ Zalo vào đoạn tạo `.env`, dùng refresh token làm chế độ bootstrap production, giữ các giới hạn media ở giá trị mặc định hiện có, kiểm tra các giá trị bắt buộc trước khi dừng container cũ và đặt quyền đọc/ghi `.env` chỉ cho owner.

## Boundaries & Constraints

**Always:** Chỉ sửa `.github/workflows/dotnet.yml`; đọc secret qua `${{ secrets.* }}`; không in giá trị secret ra log; việc kiểm tra phải diễn ra trước thao tác dừng/xóa container; giữ nguyên tên biến mà `docker-compose.prod.yml` đang tiêu thụ; giữ nguyên file test chưa track và các thư mục BMad vừa cài.

**Ask First:** Bất kỳ thay đổi nào ngoài workflow deploy, đổi chiến lược token khỏi refresh token, đổi tên GitHub Environment `production`, hoặc thay đổi quy trình build/image/VPS hiện tại.

**Never:** Không ghi giá trị secret thật vào source; không sửa `Dockerfile`, Compose, application code, database hoặc file test; không chạy deploy thật; không chạy test .NET theo yêu cầu của người dùng.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Deploy hợp lệ | Bảy Zalo secret bắt buộc có giá trị | `.env` chứa đầy đủ Zalo config, permission `600`, sau đó Compose khởi động container | Tiếp tục luồng deploy hiện tại |
| Thiếu secret bắt buộc | Một trong App/OA ID, hai secret key, refresh token, encryption key hoặc submission user ID rỗng | Không dừng container đang chạy và không gọi `docker compose up` | In tên biến bị thiếu, không in giá trị, rồi thoát lỗi |
| Secret access token tùy chọn không tồn tại | Dùng refresh-token bootstrap | Hai dòng access token/expiry để trống; refresh token được truyền xuống | Không xem đây là lỗi |

</frozen-after-approval>

## Code Map

- `.github/workflows/dotnet.yml:85-135` -- SSH deploy tạo lại `.env`, sau đó dừng container và chạy production Compose; đây là file duy nhất cần sửa.
- `docker-compose.prod.yml:57-68` -- Contract tên biến `ZALO_*` và các mặc định media mà workflow phải đáp ứng; chỉ đọc.
- `UrbanService.BLL/Services/ZaloAccessTokenProvider.cs:98-109` -- Khi refresh token có giá trị, backend ưu tiên refresh-token bootstrap và không yêu cầu access-token bootstrap; chỉ đọc.
- `UrbanService.BLL/Services/ZaloAccessTokenProvider.cs:234-251` -- Encryption key phải là Base64 của 32 byte; workflow chỉ kiểm tra không rỗng, validation định dạng vẫn thuộc backend; chỉ đọc.
- `UrbanService.BLL/Services/ZaloService.cs:997-1016` -- Submission user ID phải trỏ đến `SERVICEUSER` active; chỉ đọc.

## Tasks & Acceptance

**Execution:**
- [ ] `.github/workflows/dotnet.yml` -- thêm 12 dòng `ZALO_*` vào heredoc `.env`; lấy bảy giá trị bắt buộc từ production secrets, để access token/expiry tùy chọn theo secrets, và ghi ba mặc định media hiện có.
- [ ] `.github/workflows/dotnet.yml` -- chạy `chmod 600 .env`, sau đó kiểm tra bảy khóa bắt buộc có giá trị trước bước dừng container; thông báo lỗi chỉ nêu tên khóa.

**Acceptance Criteria:**
- Given đầy đủ production secrets, when job deploy tạo `.env`, then tất cả khóa Zalo mà production Compose tiêu thụ đều có giá trị hoặc mặc định phù hợp và file có mode `600`.
- Given một Zalo secret bắt buộc bị thiếu, when script deploy chạy validation, then job thoát lỗi trước `docker stop` và log không chứa giá trị secret.
- Given workflow dùng `ZALO_REFRESH_TOKEN`, when access token và expiry không được cấu hình, then validation vẫn thành công và backend có thể bootstrap theo nhánh refresh token.
- Given thay đổi hoàn tất, when xem diff, then chỉ workflow, spec BMad và các file setup BMad đã được người dùng cho phép xuất hiện; file test chưa track không bị sửa.

## Spec Change Log

## Verification

**Commands:**
- `git diff --check -- .github/workflows/dotnet.yml` -- expected: không có lỗi whitespace.
- Đối chiếu danh sách `ZALO_*` trong workflow với `docker-compose.prod.yml` bằng tìm kiếm read-only -- expected: không thiếu khóa Compose nào và bảy khóa bắt buộc nằm trong validation.
