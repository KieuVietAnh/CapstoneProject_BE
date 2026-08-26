# System map

These navigation facts were audited against the repository on 2026-08-26. Verify them in the current worktree before making a decision; paths and ownership can change.

## Stack and projects

- `UrbanService/`: ASP.NET Core API, controllers, middleware, DI, hosted workers, SignalR, and application configuration.
- `UrbanService.BLL/`: DTOs, interfaces, services, constraints, queues, and business rules.
- `UrbanService.DAL/`: EF entities, `UrbanServiceDbContext`, repositories/unit of work, and migrations.
- `UrbanService.BLL.Tests/`: xUnit business and integration-style unit tests.
- `UrbanService.sln`: solution entrypoint.
- All projects target .NET 9. The audited data packages are EF Core 8.0.23 and `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11; read current `.csproj` files before changing dependencies.

## Layer trace

For a behavior change, trace in this order:

1. Controller route, HTTP contract, model binding, role attribute, JWT identity extraction, and cancellation.
2. BLL interface contract and every caller/substitute.
3. BLL service validation, authorization/ownership filtering, state transition, transaction boundary, and exception behavior.
4. DAL repository/unit of work, entity navigation, `UrbanServiceDbContext` mapping, indexes, constraints, and relevant migrations/snapshot.
5. Side effects: incident events, status history, subscriptions, notifications, SignalR, uploads, AI review, webhook queue/inbox, email/message providers.
6. Focused tests, neighboring regression tests, project tests, and solution build.

Do not stop at the first matching method. Search route aliases, interface implementations, dependency-injection registrations, background workers, and tests that mock the contract.

## Audited route families

| Family | Current controller/service entry | Boundary to verify |
|---|---|---|
| `/api/management/incidents` | `ManagementIncidentsController` -> `IIncidentService` | Management roles, transition/assignment/merge rules |
| `/api/management/incident-match-candidates` | Alias on `StaffFeedbackDuplicatesController` -> `IFeedbackDuplicateCandidateService` | Current management route and legacy compatibility |
| `/api/staff/feedback-duplicates` | Legacy alias on the same controller | Do not remove or reshape silently |
| `/api/user/incidents` | `UserIncidentsController` -> `IIncidentService` | JWT user scope and subscriptions |
| `/api/public/incidents` | `PublicIncidentsController` -> `IIncidentService` | Public projection and optional current-user context |
| `/api/user/feedbacks` | `UserFeedbacksController` -> feedback/duplicate services | Owner-only operations versus explicitly anonymous feed routes |

`UserFeedbacksController` also exposes an absolute report-creation route under `/api/user/incidents/{incidentId}/reports`; include absolute route attributes when searching a slice.

## Evidence searches

Prefer narrow searches such as:

```powershell
rg -n "Route\(|Http(Get|Post|Put|Patch|Delete)|Authorize|AllowAnonymous" UrbanService/Controllers
rg -n "IIncidentService|IFeedbackService|IFeedbackDuplicateCandidateService" UrbanService.BLL UrbanService.BLL.Tests
rg -n "IncidentReportLink|IncidentSubscription|FeedbackDuplicateCandidate" UrbanService.DAL UrbanService.BLL.Tests
```

Repository documents and controller XML comments may lag implementation. Confirm claims in executable code, database mappings/migrations, and tests.
