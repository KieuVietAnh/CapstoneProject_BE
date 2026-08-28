---
title: 'Bổ sung ảnh minh chứng cho luồng phản ánh Messenger'
type: 'feature'
created: '2026-08-19'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'c921a1ed62f04d91a98eeb4d643fcaa386e54b5f'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Messenger đi thẳng từ khu vực tới xác nhận, bỏ qua attachment và luôn tạo phản ánh với danh sách tệp rỗng.

**Approach:** Thêm bước ảnh tùy chọn sau khu vực, lưu URL Meta theo draft, rồi tải có giới hạn và upload Cloudinary khi xác nhận. Tái sử dụng pattern Zalo cùng cleanup, retry và test.

## Boundaries & Constraints

**Always:** Ảnh tùy chọn; mặc định tối đa 5 ảnh, 5 MiB/ảnh và cấu hình được. Chỉ nhận `image` từ HTTPS allowlist; kiểm tra HTTP, MIME, header và stream size. Dùng payload `EVIDENCE_DONE`, `EVIDENCE_SKIP`, `CANCEL`; giữ cô lập `PageId + SenderPsid`, `GeoSource` và `SubmissionChannel = Messenger`. Không log URL/token.

**Ask First:** Bắt buộc ảnh, nhận video/file, đổi lưu trữ, thêm xóa asset Cloudinary hoặc thay queue webhook.

**Never:** Lưu binary trong DB, gắn URL Meta vào feedback cuối, nhận tệp không kiểm soát, sửa migration cũ hoặc gọi dịch vụ thật trong test.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Chuyển bước | Khu vực hợp lệ | Sang `AwaitingEvidence`; hiện Xong/Bỏ qua/Hủy | Chưa xác nhận |
| Nhận ảnh | 1..N URL hợp lệ | Lưu không trùng, giữ state, báo N/limit | Chỉ lấy slot còn lại |
| Bỏ qua/Xong | 0..N ảnh | Sang xác nhận; summary ghi số ảnh | Text khác chỉ nhắc lại |
| Ảnh sai bước | State khác | Không lưu/đổi field/state | Hướng dẫn đúng bước |
| Tệp lỗi | HTTP, host/MIME lạ, quá lớn | Không upload/create | Báo lỗi; giữ draft |
| Gửi thành công | Draft đủ, N ảnh | Upload, tạo feedback/submission, xóa draft, Completed | N=0 không tải ảnh |
| Lỗi gửi | Download/upload/create lỗi | Về xác nhận, giữ draft | Retry/MID không tạo trùng |
| Reset | Nhập lại/Menu/Hủy/API/draft thiếu | Xóa ảnh và reset | FK cascade |

</frozen-after-approval>

## Code Map

- `UrbanService.BLL/Services/MessengerService.cs:22-29,154-209,378-553,799-815` — state, webhook, submit, prompt/reset; tái dùng `ZaloService.cs:488-676,708-723,890-995,1026-1048`.
- `UrbanService.BLL/DTOs/MessengerDto.cs:60-88` — bổ sung `attachments[].payload.url`.
- `UrbanService.DAL/Entities/MessengerFeedbackConversation.cs:3-34` — navigation và entity draft attachment đối xứng Zalo.
- `UrbanService.DAL/Data/UrbanServiceDbContext.cs:54,804-880,962-988` — DbSet, mapping, FK cascade và index.
- `UrbanService.DAL/Migrations/UrbanServiceDbContextModelSnapshot.cs` — migration mới `AddMessengerFeedbackDraftAttachments`, không backfill và không sửa migration cũ.
- `FeedbackService.cs:49-89`, `ICloudinaryService.cs:5-14` — contract tạo attachment và upload.
- `MessengerServiceTests.cs:48-219` — test matrix; tham khảo `ZaloServiceTests.cs:134-245`.
- `.env.example`, `docker-compose*.yml`, `README.md:124-135` — map/document config.
- `UrbanService/Controllers/MessengerController.cs`, `UrbanService/BackgroundServices/MessengerWebhookWorker.cs` — chỉ đọc; endpoint/queue không đổi trong scope.

## Tasks & Acceptance

**Execution:**
- [x] `UrbanService.BLL/DTOs/MessengerDto.cs` — model payload URL ảnh Meta.
- [x] `UrbanService.DAL/Entities/*`, `UrbanServiceDbContext.cs`, `UrbanService.DAL/Migrations/*` — thêm draft, FK cascade và unique message/ordinal.
- [x] `UrbanService.BLL/Services/MessengerService.cs` — thêm `AwaitingEvidence`, nhận/đếm/validate/upload, summary, cleanup và recovery; inject `ICloudinaryService`.
- [x] `.env.example`, `docker-compose*.yml`, `README.md` — cấu hình `MaxImagesPerFeedback`, `MaxImageBytes`, `AllowedMediaHostSuffixes` với default an toàn.
- [x] `UrbanService.BLL.Tests/MessengerServiceTests.cs` — kiểm thử matrix và attachment truyền vào `IFeedbackService`.

**Acceptance Criteria:**
- Given hoàn tất khu vực, when bot phản hồi, then bot hỏi ảnh tùy chọn trước xác nhận.
- Given N ảnh hợp lệ, when xác nhận, then feedback có N attachment và draft được xóa.
- Given tệp/submission lỗi, when xử lý, then draft còn nguyên, không kẹt `Submitting` và retry được.
- Given reset hoặc MID trùng, when xử lý, then không còn ảnh cũ và không tạo trùng.

## Spec Change Log

## Design Notes

Giữ source URL đến lúc xác nhận để tránh asset Cloudinary mồ côi cho draft bỏ dở. Lỗi giữa chuỗi upload vẫn có thể để asset mồ côi vì interface chưa có delete; không mở rộng trong scope. Draft cũ ở `AwaitingConfirmation` vẫn xác nhận với 0 ảnh.

## Verification

**Commands:**
- `dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj --filter FullyQualifiedName~MessengerServiceTests` — tất cả test Messenger pass.
- `dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj` — toàn bộ test BLL pass.
- `dotnet build UrbanService.sln` — build thành công; không có lỗi mới.
- `dotnet ef migrations has-pending-model-changes --project UrbanService.DAL --startup-project UrbanService` — không còn model change chưa có migration.

**Manual checks (if no CLI):** payload có quick replies/summary đúng và không log URL/token.

## Suggested Review Order

**Luồng Messenger**

- Điểm vào phân loại text, ảnh và phục hồi MID theo loại sự kiện.
  [`MessengerService.cs:165`](../../UrbanService.BLL/Services/MessengerService.cs#L165)

- Bước minh chứng giới hạn slot, allowlist URL và chống lưu trùng.
  [`MessengerService.cs:495`](../../UrbanService.BLL/Services/MessengerService.cs#L495)

- Xác nhận upload ảnh, tạo feedback, cleanup và phục hồi lỗi.
  [`MessengerService.cs:684`](../../UrbanService.BLL/Services/MessengerService.cs#L684)

- Download kiểm tra redirect, timeout, HTTP, MIME, header và stream size.
  [`MessengerService.cs:1133`](../../UrbanService.BLL/Services/MessengerService.cs#L1133)

- Reset tập trung xóa ảnh nháp cho mọi đường hủy.
  [`MessengerService.cs:1293`](../../UrbanService.BLL/Services/MessengerService.cs#L1293)

**Dữ liệu và migration**

- Entity giữ URL nguồn cùng khóa idempotency message/ordinal.
  [`MessengerFeedbackDraftAttachment.cs:3`](../../UrbanService.DAL/Entities/MessengerFeedbackDraftAttachment.cs#L3)

- Mapping áp dụng cascade, index thứ tự và unique message/ordinal.
  [`UrbanServiceDbContext.cs:884`](../../UrbanService.DAL/Data/UrbanServiceDbContext.cs#L884)

- Migration chỉ tạo bảng draft mới, không backfill dữ liệu cũ.
  [`20260819103849_AddMessengerFeedbackDraftAttachments.cs:11`](../../UrbanService.DAL/Migrations/20260819103849_AddMessengerFeedbackDraftAttachments.cs#L11)

**Bảo mật và vận hành**

- Typed client chặn redirect và tắt logging URL media có token.
  [`Program.cs:41`](../../UrbanService/Program.cs#L41)

- DTO đọc `attachments[].payload.url` đúng cấu trúc webhook Meta.
  [`MessengerDto.cs:93`](../../UrbanService.BLL/DTOs/MessengerDto.cs#L93)

- Biến môi trường cung cấp giới hạn an toàn và Cloudinary bắt buộc.
  [`.env.example:7`](../../.env.example#L7)

- Workflow deploy truyền chính sách ảnh Messenger vào môi trường production.
  [`dotnet.yml:123`](../../.github/workflows/dotnet.yml#L123)

- README giải thích giới hạn, allowlist và dependency upload.
  [`README.md:130`](../../README.md#L130)

**Kiểm thử**

- Ma trận kiểm tra bước ảnh, thành công, lỗi file và retry MID.
  [`MessengerServiceTests.cs:70`](../../UrbanService.BLL.Tests/MessengerServiceTests.cs#L70)

- Test submission xác nhận attachment truyền đúng vào feedback.
  [`MessengerServiceTests.cs:338`](../../UrbanService.BLL.Tests/MessengerServiceTests.cs#L338)

- Test bảo mật phủ HTTP, MIME, timeout, size và redirect ngoài allowlist.
  [`MessengerServiceTests.cs:441`](../../UrbanService.BLL.Tests/MessengerServiceTests.cs#L441)
