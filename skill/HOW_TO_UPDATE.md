> [!WARNING]
> **Legacy/archive:** Không dùng file này làm workflow Codex chính. Native skill
> `.agents/skills/urbanservice-change/SKILL.md` và
> `references/knowledge-maintenance.md` quy định cách đề xuất/cập nhật knowledge;
> chỉ xem quy trình dưới đây như evidence legacy và không ghi nếu chưa được duyệt.

# How to Update skill/

Chỉ cập nhật `skill/` khi user yêu cầu rõ hoặc đã duyệt đề xuất lưu knowledge.

## Cấu trúc

```text
skill/
├── INDEX.md
├── HOW_TO_UPDATE.md
├── backend/
│   ├── INDEX.md
│   ├── system-map.md
│   └── feedback-workflow.md
├── bmad/
│   ├── done/
│   │   └── INDEX.md
│   ├── incident-api-plan.md
│   ├── INDEX.md
│   ├── prompts-playbook.md
│   └── platform-backlog.md
└── research/
    ├── INDEX.md
    └── <research-topic>/
        ├── INDEX.md
        ├── research.md
        ├── brief.md
        └── digests/
            └── INDEX.md
```

- Mỗi folder phải có `INDEX.md` liệt kê toàn bộ file trực tiếp bên trong.
- Parent `INDEX.md` chỉ trỏ đến `INDEX.md` của folder con.
- Root `INDEX.md` được phép deep-link file nền tảng.
- Thêm/xóa/đổi tên file phải cập nhật `INDEX.md` của folder chứa file.

## Phân loại

| Knowledge | File đích |
|---|---|
| Kiến trúc, stack, module, tích hợp | `backend/system-map.md` |
| Feedback, duplicate, SLA, trạng thái | `backend/feedback-workflow.md` |
| Quy tắc toàn repository | `INDEX.md` hoặc `AGENTS.md` nếu user yêu cầu |
| Quy trình planning/build/review | `bmad/prompts-playbook.md` |
| Năng lực cần nghiên cứu/triage | `bmad/platform-backlog.md` |
| Khảo sát, nguồn và digest đã hoàn tất | `research/<research-topic>/` |
| Lát cắt triển khai đã hoàn tất | `bmad/done/` |

## Quy trình cập nhật

1. `rg -n -i "<keyword>" skill -g '*.md'` để tìm nội dung hiện có.
2. Đối chiếu với code, migration, test và tài liệu đang có.
3. Sửa in-place; xóa nội dung sai, không thêm bản trùng.
4. Chỉ ghi rule/action hoặc baseline đã kiểm chứng từ code.
5. Giữ bullet ngắn; tách file nếu vượt 350 dòng.
6. Kiểm tra mọi `INDEX.md` khớp file thực tế.
7. Chạy lại `rg` và `git diff --check` trước khi bàn giao.

## Không được ghi

- Secret, token, connection string thật hoặc dữ liệu cá nhân.
- Suy đoán chưa kiểm chứng như một quy tắc đã chốt.
- Nội dung của repository khác hoặc stack không dùng trong UrbanService.
- Quyết định breaking change chưa được user phê duyệt.
