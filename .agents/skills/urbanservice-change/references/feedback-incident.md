# Incident and Feedback

Use this reference for Incident, Feedback/Report, matching candidates, linking, subscriptions, merge, and status changes. The facts below are audited navigation baselines, not permission to preserve or change behavior without tracing the current code.

## Database-enforced invariants

Verify the current `UrbanServiceDbContext`, latest migration, and model snapshot before relying on these:

- A Feedback can have at most one active `IncidentReportLink` (filtered unique index on `FeedbackId` where `LinkStatus = Active`).
- Link lifecycle is consistent: an active link has `UnlinkedAt = null`; an unlinked link has a non-null `UnlinkedAt`.
- An Incident/User pair has at most one `IncidentSubscription` row. Activation is represented by `IsActive`, not duplicate rows.
- An Incident cannot merge into itself.
- A Feedback marked as a master cannot have a parent; a Feedback cannot parent itself.
- A child Feedback can have at most one duplicate candidate in `Pending` or `Confirmed`; rejected history may coexist.
- A candidate pair cannot compare a Feedback with itself.

Preserve both the service validation and database guard where both exist. A green service test does not prove a constraint exists, and a database constraint does not replace a useful API error.

## Compatibility surfaces

- Management matching currently has two route families: `/api/management/incident-match-candidates` and legacy `/api/staff/feedback-duplicates`.
- Incident links preserve Report/Feedback records and audit history; unlink is a lifecycle change, not a hard delete.
- Duplicate confirmation can touch the candidate, Incident links/merge state, legacy `IsMasterTicket`/`ParentTicketId` compatibility fields, events, and subscriptions. Trace the transaction and all side effects before changing it.
- Report creation may create or link an Incident and may trigger AI review, attachments, notifications, or other asynchronous work. Trace from the controller through the service and tests rather than assuming a single write.

## Status ownership must be rediscovered

Do not encode a static answer about whether Incident or Feedback owns workflow status. Before changing a transition:

1. Inspect `IncidentConstants.cs` and relevant Feedback status constants.
2. Inspect `IIncidentService`, `IFeedbackService`, and all callers.
3. Trace `IncidentService`, `FeedbackService`, duplicate confirmation, report creation, assignment, merge, and approval paths.
4. Inspect `IncidentServiceTests`, `FeedbackMasterStatusTests`, duplicate-candidate tests, and any newly added transition tests.
5. Compare the current entity mappings and newest migrations.

Determine which aggregate is authoritative for the requested path, which compatibility write remains necessary, and which consumer reads each status. If evidence conflicts, report the conflict instead of choosing a legacy document as truth.

## Review checklist

- Is the source Report already actively linked?
- Does a target Incident exist and remain eligible for the operation?
- Can the operation create a self-link, self-candidate, or self-merge?
- Are link status and unlink audit fields updated atomically?
- Are subscriptions deduplicated and ownership scoped?
- Are candidate state transitions one-way and concurrency-safe?
- Are Incident events/status history and user notifications still correct?
- Are legacy routes and compatibility fields intentionally preserved?
- Do public/user projections expose only permitted reports and statuses?
