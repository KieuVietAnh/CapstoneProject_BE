# Research brief: Report/Request và Incident trong xử lý phản ánh đô thị

## Quyết định cần hỗ trợ

Chọn mô hình dữ liệu và lộ trình chuyển UrbanService từ `Feedback` vừa là báo cáo vừa
là đơn vị xử lý sang mô hình phân biệt rõ Report/Request và Incident.

## Câu hỏi nghiên cứu

1. Các hệ thống civic issue/311 thực tế gọi và quản lý submission, request, issue, case hoặc incident như thế nào?
2. Hệ thống nào hỗ trợ nhiều người cùng báo hoặc subscribe/follow một issue đang tồn tại?
3. Matching, duplicate linking, merge, split, reopen và human review được triển khai ra sao?
4. Trạng thái, SLA, assignment, resolution và notification thuộc report hay incident?
5. Mô hình nào phù hợp nhất với UrbanService và có thể migration an toàn từ master-feedback hiện tại?

## Phạm vi nguồn

- Ưu tiên tài liệu chính thức, API/schema docs, help center và code/docs dự án nguồn mở.
- Hệ thống mục tiêu: Open311/GeoReport v2, FixMyStreet, SeeClickFix/311 CRM, Dynamics 365 Customer Service và các hệ thống tương đương có bằng chứng công khai.
- Blog tổng hợp chỉ dùng để tìm lead; kết luận phải truy về nguồn gốc.

## Đầu ra

- So sánh mô hình thực tế và terminology.
- Gap analysis với entity/API/workflow UrbanService hiện tại.
- Recommendation và kế hoạch áp dụng theo phase, gồm data model, API, matching, migration, test và rollout.
