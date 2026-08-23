# Report/Request → Incident Models

Nghiên cứu hoàn tất ngày 2026-08-23 để trả lời góp ý review: nhiều phản ánh của
người dân có thể cùng mô tả một sự vụ và không nên tạo nhiều workflow xử lý độc lập.

## Đọc theo nhu cầu

- [`brief.md`](brief.md) — kết luận ngắn và khuyến nghị áp dụng.
- [`research.md`](research.md) — báo cáo đầy đủ cùng nguồn/citation.
- [`research-briefing.html`](research-briefing.html) — bản trình bày có thể mở bằng trình duyệt.
- [`claims.json`](claims.json) — các claim chính ở dạng dữ liệu.
- [`digests/INDEX.md`](digests/INDEX.md) — bằng chứng theo từng nhánh khảo sát.

## Kết luận đã dùng cho BE

- `Feedback` là Report/Request: giữ người gửi, kênh, nội dung và evidence gốc.
- `Incident` là sự vụ canonical: nhiều Report có thể cùng liên kết vào một Incident.
- Link phải có provenance, confidence, actor, thời điểm và lịch sử unlink.
- Người gửi Report trở thành subscriber của Incident để nhận tiến độ chung.
- AI phù hợp cho candidate/suggestion; quyết định link có rủi ro cần human review.
- Workflow, assignment, SLA và resolution nên chuyển dần sang Incident theo phase,
  không cutover đồng thời với migration schema.

## Áp dụng

- Schema foundation: [`../../bmad/done/incident-schema-foundation.md`](../../bmad/done/incident-schema-foundation.md).
- Kế hoạch API: [`../../bmad/incident-api-plan.md`](../../bmad/incident-api-plan.md).
