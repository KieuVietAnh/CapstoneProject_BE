# UrbanService Backend

UrbanService is an ASP.NET Core backend for an urban service provider platform. It offers services operated by the platform and connects users with services from external providers. The project uses a layered structure with API controllers, business services, and Entity Framework Core data access.

## Tech Stack

- .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL via Npgsql
- JWT authentication
- SignalR realtime notifications
- Swagger / OpenAPI
- Cloudinary media upload
- Docker / Docker Compose

## Solution Layout

```text
UrbanService.sln
UrbanService/          API layer: controllers, middleware, Program.cs, appsettings
UrbanService.BLL/      Business layer: DTOs, interfaces, services, common helpers
UrbanService.DAL/      Data layer: EF DbContext, entities, repositories, unit of work, migrations
DAL/                   Legacy or placeholder class library
.github/              GitHub workflow/config files
Dockerfile
docker-compose.yml
docker-compose.prod.yml
```

## Main Features

- User registration and login with JWT.
- Role-based authorization using roles such as `SERVICEUSER`, `SYSTEMADMIN`, `SYSTEMSTAFF`, `INTERACTIONMANAGER`, and `SERVICEOPERATORSTAFF`.
- Feedback CRUD for service users.
- User feedback list/detail with pagination.
- Feedback attachments stored through Cloudinary.
- Feedback status update with status history.
- Realtime notification to the feedback owner when staff or admin updates its status.
- Feedback comments.
- Feedback support / unsupport.
- Service catalog grouped by category and service operator.
- Clear distinction between system-operated and external services.
- Payment records for services operated by the system.

## Run Locally

From the solution root:

```powershell
dotnet run --project .\UrbanService\UrbanService.csproj
```

Swagger is available at the URL printed by `dotnet run`, usually:

```text
http://localhost:5219/swagger
```

or the HTTPS URL shown in the terminal.

## Configuration

The API reads configuration from `UrbanService/appsettings.json`.

Required sections:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "Jwt": {
    "Key": "...",
    "Issuer": "UrbanService",
    "Audience": "UrbanServiceClient",
    "ExpireMinutes": 60
  },
  "Cloudinary": {
    "CloudName": "...",
    "ApiKey": "...",
    "ApiSecret": "..."
  },
  "Brevo": {
    "ApiKey": "xkeysib-...",
    "SenderEmail": "your-verified-email@example.com",
    "SenderName": "UrbanService"
  },
  "GoogleAuth": {
    "ClientId": "your-google-oauth-client-id.apps.googleusercontent.com"
  }
}
```

`CloudName` must be copied from the Cloudinary dashboard credentials. It is not the local project name.

## Build

```powershell
dotnet build UrbanService.sln
```

## SignalR Notifications

Authenticated clients connect to:

```text
/hubs/notifications
```

Pass the JWT through SignalR's `accessTokenFactory` and listen for the
`NotificationReceived` event:

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => token
  })
  .withAutomaticReconnect()
  .build();

connection.on("NotificationReceived", notification => {
  console.log(notification);
});

await connection.start();
```

When `SYSTEMSTAFF` or `SYSTEMADMIN` calls
`PATCH /api/management/feedbacks/{feedbackId}/status`, the feedback owner
receives the event and the notification is stored in the database.

Notification REST APIs:

- `GET /api/notifications`: list the current user's notifications.
- `PATCH /api/notifications/{notificationId}/read`: mark one as read.
- `PATCH /api/notifications/read-all`: mark all as read.

## Database update
dotnet ef database update `
  --project .\UrbanService.DAL\UrbanService.DAL.csproj `
  --startup-project .\UrbanService\UrbanService.csproj

Last verified build: `dotnet build UrbanService.sln` completed with `0 Warning(s)` and `0 Error(s)`.
