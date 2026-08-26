# Authorization and ownership

Read this reference for route, role, JWT, ownership, visibility, or public projection work. Verify attributes and service queries in the current worktree.

## Audited boundaries

| Surface | Audited boundary |
|---|---|
| `/api/management/incidents` | `SYSTEMADMIN`, `SYSTEMSTAFF`, `INTERACTIONMANAGER` |
| Incident match candidates and legacy duplicate routes | `SYSTEMADMIN`, `SYSTEMSTAFF`, `INTERACTIONMANAGER` |
| `/api/user/incidents` | `SERVICEUSER`; current user comes from `ClaimTypes.NameIdentifier` |
| `/api/user/feedbacks` owner operations | `SERVICEUSER`; service calls receive the current user id |
| `/api/public/incidents` | Anonymous public projection; detail can receive optional current-user context |
| Explicit feedback feed routes | Some actions override the controller role with `AllowAnonymous`; verify each action |

Role attributes are only the API boundary. Owner-only reads and mutations must also constrain the BLL/DAL query by the authenticated user where applicable. Never trust a user id supplied by route, query, or body when the identity must come from JWT.

## Trace before changing

1. Enumerate all route attributes, including controller aliases and absolute action routes.
2. Record `Authorize`, `AllowAnonymous`, role constants, and the identity claim used.
3. Follow the user/role value into the BLL method and database predicate.
4. Check nested resources (attachments, comments, resolutions, related reports, subscriptions) for parent ownership and visibility.
5. Check public DTOs and projections for internal fields, hidden reports, and non-public statuses.
6. Add or update positive and negative tests: allowed role/owner, different user, anonymous caller, missing/malformed claim, and hidden/public state where relevant.

## Change boundary

Do not remove authorization, broaden roles, change claims, or alter ownership semantics unless the user explicitly requests it. For a requested change, explain affected callers and data exposure, preserve backward compatibility where possible, and state any verification gap.

Treat “not found” versus “forbidden” behavior as part of the current security/API contract. Inspect exception middleware and existing tests before changing it.
