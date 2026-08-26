# Integrations

Read this reference for Messenger, Zalo, webhooks, background workers, retries, or other external side effects. Verify configuration defaults and registrations in the current worktree without printing secret values.

## Messenger invariants

- POST webhook authenticity uses `X-Hub-Signature-256`; reject an invalid signature before enqueueing or mutating state.
- The controller should acknowledge quickly by enqueueing work; `MessengerWebhookWorker` performs scoped processing outside the request.
- Conversation state is isolated by `PageId + SenderPsid`. The current `DbContext` has a unique filtered index for that pair, and event-processing lookup uses both values; re-audit any management/read helper that accepts only a sender id.
- `LastMessageId` is the primary inbound-message idempotency guard. Preserve the order of duplicate detection, state mutation, save, and retry handling.
- Draft attachments also use source message identity/ordinal constraints; include them when changing replay behavior.

Trace webhook controller -> signature validation -> bounded queue -> worker scope -> `MessengerService` -> conversation/attachments -> Feedback creation -> outbound reply. Test invalid signatures, duplicate deliveries, page/sender isolation, retries/failures, and cancellation when relevant.

## Zalo invariants

- Zalo is disabled by default in the audited configuration.
- `ZaloWebhookWorker` is registered only when `Zalo:Enabled` is true and `Zalo:WorkerEnabled` permits it.
- A disabled webhook returns 404 before reading the body, verifying a signature, storing inbox data, enqueueing, or otherwise mutating state.
- Enabled processing uses a durable inbox/queue pattern with attempt tracking and retry behavior; trace storage and recovery before changing it.

Test both disabled and enabled branches. Do not accidentally start a worker in a disabled environment or turn a disabled endpoint into an observable/mutating endpoint.

## External-action boundary

Do not send real messages, emails, uploads, or provider requests unless explicitly authorized. Prefer mocks/fakes and focused tests. Do not print, copy, or expose tokens, app secrets, verify tokens, signatures, user identifiers, or payload contents in commands, logs, fixtures, diffs, or the final report.
