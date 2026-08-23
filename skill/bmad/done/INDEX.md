# Done — UrbanService BMAD

Chỉ lưu quyết định hoặc lát cắt đã hoàn tất. Việc chưa triển khai vẫn nằm ở
`../platform-backlog.md` hoặc tài liệu plan tương ứng.

- [`incident-schema-foundation.md`](incident-schema-foundation.md) — model Incident,
  EF mapping, migration/backfill và apply database.
- [`incident-api-p0.md`](incident-api-p0.md) — dual-write Report → Incident, API staff
  list/detail/link/unlink, duplicate relink và khóa Zalo.
- [`incident-match-workflow.md`](incident-match-workflow.md) — diễn giải duplicate thành
  Report cùng Incident, route alias mới, DTO Incident và retry idempotent.
