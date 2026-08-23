# BMAD Planning — UrbanService

Dùng cho thay đổi lớn, schema change hoặc workflow cross-module. Fix nhỏ vẫn đi theo
quy trình trong `AGENTS.md` và không cần tạo đầy đủ artifact planning.

## Vòng đời khuyến nghị

```text
Research/feedback thực địa
  → product brief hoặc PRD
  → architecture spine
  → spec/epics/stories
  → build
  → adversarial review
  → retrospective/skill update
```

## Gate bắt buộc

- Chốt problem và thuật ngữ trước khi thiết kế schema.
- Chốt ownership của trạng thái, SLA, assignment và resolution trước khi migration.
- Chốt backward compatibility của route/DTO trước khi build.
- Chốt migration/backfill/rollback trước khi đụng dữ liệu hiện hữu.
- AI chỉ hỗ trợ phân loại/đề xuất; quyết định nghiệp vụ nhạy cảm vẫn cần human review.

## Files

- [`prompts-playbook.md`](prompts-playbook.md) — chuỗi prompt áp dụng cho UrbanService.
- [`platform-backlog.md`](platform-backlog.md) — năng lực lớn đang cần triage/nghiên cứu.
- [`incident-api-plan.md`](incident-api-plan.md) — thứ tự API BE cho quá trình chuyển từ Feedback sang Incident.
- [`done/`](done/) — quyết định hoặc lát cắt đã hoàn tất, không trộn với việc còn mở.
