# UrbanService Backend

Backend ASP.NET Core cho nền tảng tiếp nhận và xử lý phản ánh đô thị. Hệ thống hỗ
trợ phản ánh từ web và Messenger, phân quyền nhân sự, SLA, thông báo realtime và
AI hỗ trợ phân loại, kiểm tra trùng lặp.

## Công nghệ

.NET 9, ASP.NET Core Web API, Entity Framework Core 8, PostgreSQL, JWT,
SignalR, Swagger, xUnit và Docker.

## Cấu trúc

```text
UrbanService/            API, controller, middleware và cấu hình DI
UrbanService.BLL/        DTO, interface và business service
UrbanService.DAL/        Entity, DbContext, repository và migration
UrbanService.BLL.Tests/  Unit test
```

## Chạy local

Yêu cầu:

- .NET SDK 9
- PostgreSQL và một database có thể truy cập
- EF Core CLI 8.0.23

Cài công cụ và restore package:

```powershell
dotnet tool update --global dotnet-ef --version 8.0.23
dotnet restore
```

Nếu chưa từng cài `dotnet-ef`, dùng `dotnet tool install` thay cho `update`.

### 1. Cấu hình

Tạo `UrbanService/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=urban_service;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Jwt": {
    "Key": "YOUR_LOCAL_JWT_KEY_AT_LEAST_32_CHARACTERS",
    "Issuer": "UrbanService",
    "Audience": "UrbanServiceClient",
    "ExpireMinutes": 60,
    "RefreshTokenExpireDays": 7
  }
}
```

File này đã được `.gitignore`. Không đưa password hoặc token thật vào Git.

Có thể dùng biến môi trường thay thế:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=urban_service;Username=postgres;Password=YOUR_PASSWORD"
$env:Jwt__Key = "YOUR_LOCAL_JWT_KEY_AT_LEAST_32_CHARACTERS"
```

### 2. Cập nhật database

```powershell
dotnet ef database update `
  --project .\UrbanService.DAL\UrbanService.DAL.csproj `
  --startup-project .\UrbanService\UrbanService.csproj
```

### 3. Chạy API

```powershell
dotnet run --project .\UrbanService\UrbanService.csproj --launch-profile http
```

- Swagger: `http://localhost:5219/swagger`
- Health check: `http://localhost:5219/health`
- SignalR notification hub: `/hubs/notifications`

## Build và test

```powershell
dotnet build UrbanService.sln
dotnet test UrbanService.sln
```

Khi sửa entity hoặc EF mapping, tạo migration mới bằng `dotnet ef migrations add`
và chạy test trước khi commit.

## Chạy bằng Docker

Tạo `.env` ở root:

```dotenv
DEFAULT_CONNECTION=Host=host.docker.internal;Port=5432;Database=urban_service;Username=postgres;Password=YOUR_PASSWORD
JWT_KEY=YOUR_LOCAL_JWT_KEY_AT_LEAST_32_CHARACTERS
JWT_ISSUER=UrbanService
JWT_AUDIENCE=UrbanServiceClient
DATABASE_MIGRATE_ON_STARTUP=true
```

Chạy backend:

```powershell
docker compose up --build
```

Swagger nằm tại `http://localhost:8080/swagger`. Compose chỉ chạy backend, không
tạo PostgreSQL. Nếu database chạy trong cùng Docker network, dùng tên service
database thay cho `host.docker.internal`.

## Tích hợp tùy chọn

| Tính năng | Section cấu hình |
| --- | --- |
| Upload ảnh | `Cloudinary` |
| Email | `Brevo` |
| SMS OTP xác thực SĐT | `Firebase`, `PhoneOtp` |
| Google login | `GoogleAuth` |
| AI | `AI`, `OpenRouter` |
| Messenger bot | `Messenger` |
| Zalo OA bot | `Zalo` |
| Theo dõi SLA | `SlaMonitoring` |

Xem tên biến môi trường Docker trong [docker-compose.yml](docker-compose.yml).

Tài khoản được xác thực bằng **OTP gửi qua SMS tới số điện thoại**, dùng Firebase
Phone Authentication. Email vẫn bắt buộc khi đăng ký nhưng dành cho luồng quên mật
khẩu, vì gửi email gần như không tốn gì trong khi mỗi tin SMS là chi phí thật.

Backend **không gửi SMS**. Firebase gửi từ phía client; backend xác minh Firebase ID
token bằng Admin SDK rồi lấy số điện thoại **từ trong token đã kiểm tra chữ ký**,
không tin số client gửi lên — nếu không thì gửi đại một số là qua cửa.

```text
POST /api/auth/register                      -> tạo tài khoản, trả JWT (isVerified=false), KHÔNG gửi OTP
POST /api/auth/google-login                  -> lần đầu tự tạo tài khoản, isVerified=false
POST /api/auth/phone-verification/request-otp -> [Authorize] xin phép gửi SMS, kiểm tra hạn mức
POST /api/auth/phone-verification/verify      -> [Authorize] gửi Firebase ID token, isVerified=true
PATCH /api/auth/pending-account               -> [Authorize] sửa thông tin tài khoản chưa xác thực
```

`request-otp` không gửi gì cả: nó kiểm tra số hợp lệ, chưa thuộc tài khoản đã xác
thực khác, và hôm nay còn hạn mức, rồi ghi một lượt vào `phone_otp_requests`. Client
chỉ gọi `signInWithPhoneNumber` của Firebase **sau khi** endpoint này trả `200`. Hạn
mức phải nằm ở backend vì API key Firebase của web là công khai; chặn ở frontend thì
người gọi thẳng Firebase vẫn đốt tiền.

| Hạn mức | Mặc định | Phạm vi | Cấu hình |
| --- | --- | --- | --- |
| SMS OTP | 5 / ngày | Toàn hệ thống, giờ Việt Nam | `PhoneOtp:DailyLimit` |
| Gửi phản ánh | 3 / ngày | Mỗi tài khoản, chỉ kênh Web | `FeedbackLimits:DailyPerUser` |

Số khai trong `PhoneOtp:TestNumbers` là số test của Firebase Console: dùng mã cố
định, không phát sinh SMS thật nên **không tính vào hạn mức**. Dùng chúng để demo.

Xác thực **không** phải điều kiện để đăng nhập, mà là điều kiện để **ghi dữ liệu**.
Người dùng chưa xác thực vẫn đăng nhập, xem bảng tin, bản đồ sự cố và nhận thông báo
bình thường; mọi thao tác ghi bị chặn với `403` và `data.code = PHONE_NOT_VERIFIED`.

Ràng buộc nằm ở `PhoneVerifiedWriteFilter`, đăng ký toàn cục nên không endpoint ghi
nào lọt lưới. Filter chỉ áp cho role `SERVICEUSER`: tài khoản nội bộ do admin tạo có
`is_verified` mặc định `false`, áp cho mọi role sẽ khóa sạch thao tác của staff,
manager và admin ngay lúc deploy. Các API hoàn tất đăng ký được đánh dấu
`[AllowUnverifiedPhone]` nên vẫn gọi được, nếu không người dùng sẽ không có đường
nào để tự xác thực.

Đăng nhập bằng tài khoản chưa xác thực vẫn trả `200` và vẫn có token, nhưng body
khác:

```jsonc
{
  "code": "PHONE_NOT_VERIFIED",
  "message": "Số điện thoại chưa được xác thực.",
  "token": "...",
  "refreshToken": "...",
  "user": { "id": "...", "email": "...", "fullName": "...", "phoneNumber": "...", "role": "SERVICEUSER", "isVerified": false }
}
```

`pending-account` dùng cho trường hợp gõ nhầm thông tin lúc đăng ký và muốn quay lại
sửa. Giữ nguyên email hoặc số điện thoại của chính tài khoản thì không báo trùng;
đổi sang giá trị của tài khoản khác thì trả `400`. Endpoint này không gửi OTP —
người dùng bấm gửi mã ở màn xác thực, nơi hạn mức được đếm. Response trả JWT mới.

Đăng ký lại bằng email của một tài khoản **chưa xác thực** sẽ cập nhật tên, số điện
thoại và mật khẩu rồi trả token mới, thay vì báo trùng email khiến người bỏ dở giữa
chừng kẹt vĩnh viễn; tài khoản đã xác thực hoặc đã bị khóa thì vẫn báo
`Email đã được sử dụng.` Đăng nhập Google tạo tài khoản với `isVerified = false`:
Google chứng minh email chứ không chứng minh số điện thoại, và tài khoản kiểu này
còn chưa có số nào.

Một số điện thoại chỉ thuộc về một tài khoản đã xác thực. Ràng buộc đặt ở tầng
nghiệp vụ chứ không phải unique index, vì dữ liệu hiện có có thể đã trùng số mà
migration lỗi thì container mới không khởi động được.

Luồng quên mật khẩu vẫn đi qua **email**, tách làm ba bước, OTP chỉ bị tiêu thụ ở
bước cuối:

```text
POST /api/auth/forgot-password/send-otp   -> gửi OTP tới email, luôn trả 204
POST /api/auth/forgot-password/verify-otp -> kiểm tra OTP, không tiêu thụ
POST /api/auth/forgot-password/reset      -> đổi mật khẩu, tiêu thụ OTP, thu hồi refresh token
```

Phản ánh từ Messenger chỉ được tạo sau khi người gửi liên kết Messenger với một
tài khoản `SERVICEUSER` đã xác thực và có số điện thoại. Bot gửi liên kết dùng một
lần tới trang frontend; frontend đăng nhập rồi gọi
`POST /api/user/messenger-links/confirm` để hoàn tất liên kết.

Messenger cần `PageAccessToken`, `VerifyToken`, `AppSecret`,
`AccountLinkBaseUrl`, `AccountLinkTokenMinutes` và `GraphApiVersion`. Ảnh minh chứng tùy chọn được giới hạn bởi
`MaxImagesPerFeedback` (mặc định 5), `MaxImageBytes` (mặc định 5 MiB) và chỉ tải
từ các hậu tố HTTPS trong `AllowedMediaHostSuffixes` (mặc định
`fbcdn.net,fbsbx.com`). Cần cấu hình thêm `Cloudinary:CloudName`,
`Cloudinary:ApiKey` và `Cloudinary:ApiSecret` để upload ảnh khi xác nhận.
`AccountLinkBaseUrl` phải trỏ tới trang frontend nhận query parameter `token`.
Webhook cần cấu hình trên Meta:

```text
https://YOUR_DOMAIN/api/integrations/messenger/webhook
```

Page cần subscribe `messages` và `messaging_postbacks`.

Zalo OA cần `AppId`, `AppSecretKey`, `OaId`, `OaSecretKey`, `SubmissionUserId`
và `TokenEncryptionKey`. Có thể bootstrap bằng `RefreshToken` hoặc bằng `AccessToken`
kèm `AccessTokenExpiresAtUtc`; token mới sẽ được mã hóa và lưu trong database.
`SubmissionUserId` phải thuộc một `SERVICEUSER` đang hoạt động. Webhook cần dùng HTTPS:

```text
https://YOUR_DOMAIN/api/integrations/zalo/webhook
```

Trong Zalo Developers, cấp quyền gửi tin, quản lý tin nhắn và nhận sự kiện tin nhắn;
bật các event `user_send_text`, `user_send_image`, `user_send_location`. Không bật
`Lọc cú pháp` nếu muốn tiếp nhận tin nhắn không bắt đầu bằng `#`.

Tạo khóa mã hóa token 32 byte bằng PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

## Lỗi thường gặp

### `Host can't be null`

Connection string đang trống. Kiểm tra `appsettings.Development.json` hoặc:

```powershell
$env:ConnectionStrings__DefaultConnection
```

### Phiên bản EF CLI cũ

```powershell
dotnet tool update --global dotnet-ef --version 8.0.23
```

### Migration không được nhận diện

```powershell
dotnet build UrbanService.sln
dotnet ef database update --project UrbanService.DAL --startup-project UrbanService
```

## Đóng góp

Đọc [AGENTS.md](AGENTS.md) trước khi dùng AI agent hoặc sửa code. Không commit
secret, `.env`, `appsettings.Development.json`, `bin/obj` hoặc dữ liệu database.
