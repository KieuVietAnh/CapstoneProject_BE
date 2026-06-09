# Project Structure

This document summarizes the UrbanService backend structure discovered from the current workspace.

## Root

```text
UrbanService/
.github/
UrbanService.BLL/
UrbanService.DAL/
Dockerfile
docker-compose.yml
docker-compose.prod.yml
UrbanService.sln
```

## Projects

### `UrbanService`

ASP.NET Core Web API project.

Important files and folders:

```text
UrbanService/Program.cs
UrbanService/Controllers/
UrbanService/Middlewares/
UrbanService/Filters/
UrbanService/Properties/
UrbanService/appsettings.json
UrbanService/UrbanService.csproj
```

Responsibilities:

- Configures controllers.
- Registers `UrbanServiceDbContext`.
- Registers BLL services.
- Configures JWT bearer authentication.
- Configures Swagger.
- Adds exception middleware.
- Maps API controllers.

Known controllers:

- `AuthController`
- `UserFeedbacksController`

### `UrbanService.BLL`

Business logic layer.

Important folders:

```text
UrbanService.BLL/DTOs/
UrbanService.BLL/Interfaces/
UrbanService.BLL/Services/
UrbanService.BLL/Common/
```

Known services:

- `AuthService`
- `JwtTokenGenerator`
- `FeedbackService`
- `CloudinaryService`

Known interfaces:

- `IAuthService`
- `IJwtTokenGenerator`
- `IFeedbackService`
- `ICloudinaryService`

Common helpers:

- `ApiResponse<T>`
- `PasswordHasher`
- `UserRole`

### `UrbanService.DAL`

Data access layer using Entity Framework Core.

Important folders:

```text
UrbanService.DAL/Data/
UrbanService.DAL/Entities/
UrbanService.DAL/Repositories/
UrbanService.DAL/Interfaces/
UrbanService.DAL/UnitOfWork/
UrbanService.DAL/Migrations/
```

Important files:

- `UrbanService.DAL/Data/UrbanServiceDbContext.cs`
- `UrbanService.DAL/Repositories/GenericRepository.cs`
- `UrbanService.DAL/UnitOfWork/UnitOfWork.cs`
- `UrbanService.DAL/Interfaces/IGenericRepository.cs`
- `UrbanService.DAL/Interfaces/IUnitOfWork.cs`

The repository pattern exposes generic access through:

```csharp
_uow.GetRepository<TEntity>()
```

## Main Entities

Feedback-related entities already exist in `UrbanService.DAL/Entities`:

- `Feedback`
- `FeedbackAttachment`
- `FeedbackStatusHistory`
- `FeedbackComment`
- `FeedbackSupport`
- `FeedbackAssignment`
- `FeedbackResolution`
- `FeedbackResolutionAttachment`
- `FeedbackResolutionReview`

Other important entities:

- `User`
- `Role`
- `UrbanServiceCategory`
- `ServiceOperator`
- `Notification`
- `Channel`
- `InteractionMessage`
- `MessageAttachment`
- `AiConversation`
- `AiMessage`
- `AiKnowledgeSource`
- `AnalysisResult`

## Feedback Feature Structure

New user feedback API pieces:

```text
UrbanService/Controllers/UserFeedbacksController.cs
UrbanService.BLL/DTOs/FeedbackDto.cs
UrbanService.BLL/DTOs/PagedResultDto.cs
UrbanService.BLL/DTOs/CloudinaryDto.cs
UrbanService.BLL/Interfaces/IFeedbackService.cs
UrbanService.BLL/Interfaces/ICloudinaryService.cs
UrbanService.BLL/Services/FeedbackService.cs
UrbanService.BLL/Services/CloudinaryService.cs
```

Base route:

```text
api/user/feedbacks
```

Implemented endpoints:

```text
GET    /api/user/feedbacks
GET    /api/user/feedbacks/{feedbackId}
POST   /api/user/feedbacks
PUT    /api/user/feedbacks/{feedbackId}
DELETE /api/user/feedbacks/{feedbackId}
POST   /api/user/feedbacks/{feedbackId}/attachments
DELETE /api/user/feedbacks/{feedbackId}/attachments/{attachmentId}
PATCH  /api/user/feedbacks/{feedbackId}/status
POST   /api/user/feedbacks/{feedbackId}/comments
POST   /api/user/feedbacks/{feedbackId}/support
DELETE /api/user/feedbacks/{feedbackId}/support
```

Authorization:

- Controller requires JWT authentication.
- Controller is restricted to `SERVICEUSER`.
- User id is read from `ClaimTypes.NameIdentifier`.

Pagination:

- Feedback list accepts `pageNumber` and `pageSize`.
- Service returns `PagedResultDto<T>` with `items`, `pageNumber`, `pageSize`, `totalItems`, `totalPages`, `hasPreviousPage`, and `hasNextPage`.

Cloudinary:

- `CloudinaryService` uploads image, video, and raw files.
- Feedback attachment URLs are stored in `feedback_attachments.file_url`.
- Required config keys:

```text
Cloudinary:CloudName
Cloudinary:ApiKey
Cloudinary:ApiSecret
```

## Runtime Notes

Run from solution root:

```powershell
dotnet run --project .\UrbanService\UrbanService.csproj
```

Build from solution root:

```powershell
dotnet build UrbanService.sln
```

The direct command below fails from the solution root because the API `.csproj` is inside the `UrbanService` folder:

```powershell
dotnet run --project .\UrbanService.csproj
```

Use this instead:

```powershell
dotnet run --project .\UrbanService\UrbanService.csproj
```
