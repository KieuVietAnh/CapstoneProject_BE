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
| Google login | `GoogleAuth` |
| AI | `AI`, `OpenRouter` |
| Messenger bot | `Messenger` |
| Zalo OA bot | `Zalo` |
| Theo dõi SLA | `SlaMonitoring` |

Xem tên biến môi trường Docker trong [docker-compose.yml](docker-compose.yml).

Messenger cần `PageAccessToken`, `VerifyToken`, `AppSecret`, `SubmissionUserId`
và `GraphApiVersion`. Ảnh minh chứng tùy chọn được giới hạn bởi
`MaxImagesPerFeedback` (mặc định 5), `MaxImageBytes` (mặc định 5 MiB) và chỉ tải
từ các hậu tố HTTPS trong `AllowedMediaHostSuffixes` (mặc định
`fbcdn.net,fbsbx.com`). Cần cấu hình thêm `Cloudinary:CloudName`,
`Cloudinary:ApiKey` và `Cloudinary:ApiSecret` để upload ảnh khi xác nhận.
`SubmissionUserId` phải thuộc một `SERVICEUSER` đang hoạt động. Webhook cần cấu
hình trên Meta:

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
