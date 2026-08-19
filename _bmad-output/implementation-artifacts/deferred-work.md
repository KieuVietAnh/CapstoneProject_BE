- source_spec: `C:\Users\CytekPC\OneDrive\Desktop\SEP491\BE\UrbanService\_bmad-output\implementation-artifacts\spec-messenger-evidence-images.md`
  summary: Bổ sung khóa idempotency bền vững cho việc tạo feedback từ các kênh tích hợp.
  evidence: `FeedbackService.CreateAsync` lưu feedback trước các tác vụ hậu xử lý và có thể ném lỗi trước khi trả ID, nên caller không thể phân biệt lỗi trước hay sau commit để retry mà không có nguy cơ tạo trùng.
- source_spec: `C:\Users\CytekPC\OneDrive\Desktop\SEP491\BE\UrbanService\_bmad-output\implementation-artifacts\spec-messenger-evidence-images.md`
  summary: Tuần tự hóa cập nhật draft và xác nhận Messenger giữa nhiều instance.
  evidence: Worker hiện chỉ tuần tự trong một process; các chuỗi đọc-đếm-thêm ảnh và chuyển `AwaitingConfirmation` sang `Submitting` chưa có khóa hoặc compare-and-set ở database.
- source_spec: `C:\Users\CytekPC\OneDrive\Desktop\SEP491\BE\UrbanService\_bmad-output\implementation-artifacts\spec-messenger-evidence-images.md`
  summary: Bổ sung `PageId` vào API quản trị xem và reset hội thoại Messenger.
  evidence: Webhook cô lập theo `PageId + SenderPsid`, nhưng API quản trị hiện chỉ nhận `SenderPsid`, nên môi trường nhiều Page có thể xem hoặc reset nhầm hội thoại.
- source_spec: `C:\Users\CytekPC\OneDrive\Desktop\SEP491\BE\UrbanService\_bmad-output\implementation-artifacts\spec-messenger-evidence-images.md`
  summary: Cô lập lỗi theo từng sự kiện trong một payload webhook Messenger.
  evidence: `ProcessWebhookAsync` xử lý tuần tự nhiều sự kiện nhưng chưa có ranh giới lỗi cho từng sự kiện, nên một lỗi có thể ngăn các sự kiện hợp lệ phía sau trong cùng payload được xử lý.
