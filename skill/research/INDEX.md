# Research — UrbanService Backend

Kho lưu các khảo sát đã hoàn tất phục vụ quyết định kiến trúc và nghiệp vụ BE.
Research phải giữ nguồn/citation, kết luận và phạm vi áp dụng; kế hoạch chưa triển khai
vẫn nằm trong `../bmad/`.

## Nghiên cứu hiện có

- [`report-incident-models/`](report-incident-models/) — khảo sát mô hình
  Report/Request → Incident trong civic reporting, 311 và case management; bao gồm
  báo cáo đầy đủ, brief, briefing HTML, claims và các digest chuyên đề.

## Quy ước lưu trữ

- Một chủ đề dùng một thư mục riêng, tên kebab-case.
- `research.md` là báo cáo đầy đủ; `brief.md` là bản đọc nhanh.
- `research-briefing.html` dùng để trình bày trực quan khi có sẵn.
- `digests/` chứa bằng chứng theo từng nhánh khảo sát.
- Không lưu `.memlog`, script render, cache hoặc bản digest trùng nội dung.
