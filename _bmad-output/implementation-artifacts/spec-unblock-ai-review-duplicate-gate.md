---
title: 'Gỡ chặn AI review bởi duplicate classification'
type: 'bugfix'
created: '2026-08-17'
status: 'done'
review_loop_iteration: 0
baseline_commit: '69e4e7e3e167ebbf183ebd547490ff8aecd55276'
context: []
---

<frozen-after-approval reason="human-owned intent â€” do not modify unless human renegotiates">

## Intent

**Problem:** Background worker hiện coi duplicate classification chưa hoàn tất là điều kiện bắt buộc trước AI review chính. Khi một feedback cũ trong cùng khu vực bị kẹt, các feedback mới đều giữ `Submitted`, không được gọi Qwen và retry lặp lại.

**Approach:** Tách AI review category/priority khỏi duplicate classification. Duplicate classification vẫn giữ quy tắc và ràng buộc hiện có, nhưng kết quả chưa hoàn tất không được làm mất cơ hội AI review chính. Các trường hợp duplicate chưa hoàn tất phải được retry độc lập, không làm đổi route, status contract hoặc schema công khai.

## Boundaries & Constraints

**Always:** Giữ `FeedbackDuplicateCandidate` và trạng thái `Pending`/`Confirmed` làm nguồn sự thật; giữ advisory lock theo khu vực, canonical master invariant, retry cooldown và log có feedback ID; feedback `Submitted` chỉ được AI analysis một lần thành công và chuyển `AiReviewed` như hiện tại.

**Ask First:** Không cần quyết định mới nếu chỉ sửa worker và test. Nếu phát hiện cần migration, đổi API công khai, đổi quyền hoặc thay workflow staff, phải dừng và hỏi người dùng.

**Never:** Không tự sửa dữ liệu production, không xóa candidate/lịch sử, không bỏ authorization, không cho feedback có parent tự chạy AI review riêng, không đổi model hoặc cấu hình AI.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Feedback mới bị older unresolved chặn duplicate | `Submitted`, chưa master/parent, duplicate service defer | AI review chính vẫn được gọi và chuyển `AiReviewed`; duplicate classification được ghi log và retry riêng | Không ném lỗi làm kẹt AI review |
| Duplicate candidate Pending/Confirmed | Feedback đã có candidate active | Không gọi duplicate classifier lần nữa; AI review chính vẫn xử lý nếu còn `Submitted` | Giữ candidate để staff xử lý |
| AI review chính lỗi | Duplicate đã xử lý hoặc đang defer | Feedback giữ `Submitted`, retry theo cooldown hiện tại | Log lỗi AI hiện hữu |
| Feedback đã `AiReviewed` còn duplicate unresolved | Candidate chưa có, chưa master/parent | Worker chỉ retry duplicate classification, không gọi lại AI analysis | Log retry duplicate, không tạo analysis trùng |

</frozen-after-approval>

## Code Map

- `UrbanService/BackgroundServices/AiFeedbackReviewWorker.cs` -- scan `Submitted`, dequeue và hiện đang throw khi duplicate classification chưa hoàn tất; cần tách nhánh duplicate retry và AI analysis.
- `UrbanService.BLL/Services/AiFeedbackDuplicateService.cs` -- giữ nguyên logic phát hiện candidate, defer theo khu vực và canonical master; chỉ được gọi best-effort từ worker.
- `UrbanService.BLL/Services/AiFeedbackAnalysisService.cs` -- chỉ nhận `Submitted`, tạo `AnalysisResult` và chuyển status sang `AiReviewed`; không đổi contract.
- `UrbanService.BLL.Tests/AiFeedbackDuplicateServiceTests.cs` -- test hiện hành mô tả older unresolved defer; giữ để bảo vệ invariant duplicate.
- `UrbanService.BLL.Tests/` -- thêm test worker hoặc test helper phù hợp cho việc AI review không bị duplicate gate và không chạy analysis lặp ở `AiReviewed`.

## Tasks & Acceptance

**Execution:**
- [x] `UrbanService/BackgroundServices/AiFeedbackReviewWorker.cs` -- tách duplicate classification khỏi hard-block AI review; scan/retry `AiReviewed` unresolved chỉ cho duplicate, giữ cooldown và log rõ ràng.
- [x] `UrbanService.BLL.Tests/` -- thêm/cập nhật test cho older unresolved, candidate active, AI review failure và không gọi analysis lặp.

**Acceptance Criteria:**
- Given feedback `Submitted` bị defer vì older unresolved trong cùng area, when worker xử lý, then AI client vẫn được gọi cho review chính và feedback có thể chuyển `AiReviewed`.
- Given feedback đã `AiReviewed` nhưng duplicate chưa hoàn tất, when scanner retry, then chỉ duplicate classifier chạy và không tạo `AnalysisResult` mới.
- Given duplicate candidate `Pending` hoặc `Confirmed`, when worker xử lý, then không tạo candidate thứ hai và AI review chính không bị chặn.
- Given AI review chính timeout/lỗi, when worker xử lý, then feedback vẫn `Submitted` và retry theo delay hiện tại.

## Design Notes

Duplicate classification vẫn có thể cần staff xác nhận; việc AI review chính hoàn tất không tự động xác nhận duplicate. Worker chỉ coi duplicate là một nhánh xử lý độc lập, nhờ đó lỗi hoặc backlog duplicate không tạo starvation cho SLA tiếp nhận và phân loại.

## Verification

**Commands:**
- `dotnet test UrbanService.BLL.Tests/UrbanService.BLL.Tests.csproj` -- expected: all tests pass.
- `dotnet build UrbanService.sln` -- expected: build succeeds without new errors.
- `git diff --check` -- expected: no whitespace errors.



## Suggested Review Order

**Worker orchestration**

- Tách scan `Submitted` và `AiReviewed` unresolved để retry duplicate mà không lặp AI analysis.
  [`AiFeedbackReviewWorker.cs:72`](../../UrbanService/BackgroundServices/AiFeedbackReviewWorker.cs#L72)

- Cho duplicate classification chạy best-effort, rồi quyết định riêng việc gọi AI review.
  [`AiFeedbackReviewWorker.cs:156`](../../UrbanService/BackgroundServices/AiFeedbackReviewWorker.cs#L156)

- Ghi nhận retry duplicate và phân biệt log lỗi AI với lỗi duplicate.
  [`AiFeedbackReviewWorker.cs:198`](../../UrbanService/BackgroundServices/AiFeedbackReviewWorker.cs#L198)

**Verification**

- Kiểm tra `Submitted` deferred vẫn gọi analysis đúng một lần.
  [`AiFeedbackReviewWorkerTests.cs:19`](../../UrbanService.BLL.Tests/AiFeedbackReviewWorkerTests.cs#L19)

- Kiểm tra `AiReviewed` unresolved không tạo analysis lần hai.
  [`AiFeedbackReviewWorkerTests.cs:61`](../../UrbanService.BLL.Tests/AiFeedbackReviewWorkerTests.cs#L61)
