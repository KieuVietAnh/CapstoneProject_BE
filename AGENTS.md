# Hướng dẫn cho AI agent

## Phạm vi áp dụng

File này áp dụng cho toàn bộ repository `UrbanService`. Nếu một thư mục con có
`AGENTS.md` riêng thì hướng dẫn ở thư mục con được ưu tiên trong phạm vi đó.

## Mục tiêu project

UrbanService là backend cho nền tảng tiếp nhận và xử lý vấn đề đô thị. Hệ thống
cho phép người dân gửi phản ánh từ web hoặc Messenger, hỗ trợ nhân sự tiếp nhận,
phân loại, xác minh, phân công, theo dõi SLA, phối hợp nhà cung cấp và thông báo
tiến độ. AI được dùng để hỗ trợ phân loại, kiểm tra trùng lặp và soạn nội dung,
nhưng không được thay thế các quyết định nghiệp vụ cần con người phê duyệt.

Các mục tiêu ưu tiên:

- Giữ dữ liệu phản ánh chính xác, có lịch sử và truy vết được.
- Không làm lộ dữ liệu giữa người dùng, khu vực hoặc kênh tiếp nhận.
- Bảo toàn workflow trạng thái, phân quyền và thời hạn SLA.
- Duy trì API ổn định cho frontend, Messenger và các tích hợp bên ngoài.
- Ưu tiên khả năng vận hành, quan sát lỗi và phục hồi hơn các tối ưu phức tạp.

## Đối tượng người dùng

- `SERVICEUSER`: người dân tạo, theo dõi và tương tác với phản ánh.
- `SYSTEMSTAFF`: nhân viên tiếp nhận, xác minh và xử lý nghiệp vụ.
- `SYSTEMADMIN`: quản trị tài khoản, cấu hình và dữ liệu toàn hệ thống.
- `INTERACTIONMANAGER`: quản lý tương tác và hội thoại với người dân.
- `SERVICEOPERATORSTAFF`: nhân sự thuộc đơn vị cung cấp dịch vụ.
- Nhóm phát triển và vận hành API, database, AI, Messenger và thông báo realtime.

## Kiến trúc cần giữ

- `UrbanService/`: API layer, controller, middleware, SignalR hub, cấu hình DI.
- `UrbanService.BLL/`: DTO, interface, business service, constraint và queue.
- `UrbanService.DAL/`: entity, `DbContext`, repository, unit of work và migration.
- `UrbanService.BLL.Tests/`: unit test cho business logic.

Controller phải mỏng: nhận request, kiểm tra quyền ở biên API và gọi service.
Business rule nằm trong BLL. Entity, mapping EF và migration nằm trong DAL. Không
đưa truy vấn database hoặc nghiệp vụ dài trực tiếp vào controller.

## Phong cách giao diện và nội dung

Repository hiện là backend. Khi thay đổi API hoặc nội dung hiển thị cho frontend,
Swagger, email, thông báo hay Messenger, áp dụng các nguyên tắc sau:

- Giao diện nghiệp vụ phải rõ ràng, gọn, dễ quét và ưu tiên thao tác thường dùng.
- Màn hình nhân sự mang phong cách công cụ vận hành, mật độ thông tin hợp lý,
  trạng thái dễ so sánh; không thiết kế như landing page quảng cáo.
- Luồng người dân phải dùng ngôn ngữ đơn giản, câu hỏi ngắn và chỉ yêu cầu một
  hành động chính tại mỗi bước.
- Nội dung gửi người dùng dùng tiếng Việt tự nhiên, nhất quán thuật ngữ
  `phản ánh`, `khu vực`, `trạng thái`, `xác nhận`.
- Màu sắc không được là tín hiệu duy nhất cho trạng thái. Luôn có nhãn hoặc icon.
- Thiết kế mới phải responsive, hỗ trợ bàn phím và không để nội dung bị tràn.
- Không tự ý đổi tên trạng thái nghiệp vụ chỉ để phù hợp với câu chữ giao diện;
  dùng lớp ánh xạ nhãn hiển thị nếu cần.

## Quy tắc code

- Dùng .NET 9, ASP.NET Core, EF Core và PostgreSQL theo phiên bản hiện có.
- Tuân theo style C# hiện tại: nullable enabled, file-scoped namespace, async/await
  và dependency injection qua interface.
- Ưu tiên pattern và helper đã có. Chỉ tạo abstraction khi nó giảm lặp hoặc làm rõ
  ownership thực sự.
- Truyền `CancellationToken` qua các luồng I/O khi contract hiện có hỗ trợ.
- Dùng truy vấn LINQ có thể thực thi ở database; tránh tải toàn bộ bảng để lọc trong RAM.
- Dùng constant hiện có cho role, trạng thái, kênh gửi và giá trị nghiệp vụ.
- Thời gian nghiệp vụ được tạo bằng `DateTime.UtcNow`, phù hợp quy ước hiện tại.
- API mới phải có DTO rõ ràng. Không trả trực tiếp EF entity từ controller.
- Thay đổi schema phải có EF migration và cập nhật model snapshot. Migration đổi
  dữ liệu phải có chiến lược backfill an toàn và `Down` hợp lý.
- Không sửa migration cũ đã có khả năng được áp dụng ở môi trường dùng chung.
- Không ghi secret, token, password, connection string thật hoặc dữ liệu cá nhân
  vào source, log, test fixture, README hay câu trả lời.
- Thông báo lỗi log ở server phải đủ ngữ cảnh nhưng không chứa secret. Nội dung trả
  cho client không được lộ stack trace hoặc chi tiết hạ tầng.
- Giữ tương thích ngược cho route, DTO và giá trị enum/string công khai. Nếu bắt
  buộc breaking change, phải nêu rõ và chờ người dùng xác nhận trước khi sửa.
- Thêm hoặc cập nhật test theo rủi ro. Tối thiểu chạy test project bị ảnh hưởng và
  build solution trước khi bàn giao.

## Quy tắc riêng cho Messenger

- Luôn xác minh webhook signature và giữ phản hồi webhook nhanh qua queue/worker.
- Dùng `PageId + SenderPsid` để cô lập hội thoại; không truy vấn lịch sử chỉ bằng
  tài khoản `Messenger:SubmissionUserId` dùng chung.
- Quick Reply phải có payload ổn định, không phụ thuộc hoàn toàn vào nhãn tiếng Việt.
- Giữ cơ chế chống xử lý trùng `LastMessageId` và xem xét retry trước khi đổi thứ tự lưu.
- Feedback từ Messenger phải có `SubmissionChannel = Messenger`; web mặc định là `Web`.
- Không gửi tin chủ động ngoài phạm vi chính sách Messenger nếu chưa được yêu cầu.

## Không được tự ý thay đổi

AI agent không được tự ý thực hiện các việc sau nếu người dùng chưa yêu cầu rõ:

- Đổi role, ma trận phân quyền, JWT claim hoặc bỏ authorization khỏi endpoint.
- Đổi workflow feedback, điều kiện chuyển trạng thái, SLA, duplicate linking hoặc
  logic phê duyệt.
- Xóa bảng, cột, migration, dữ liệu, attachment hoặc lịch sử trạng thái.
- Đổi route, HTTP method, response shape hoặc tên field API đang công khai.
- Đổi nhà cung cấp PostgreSQL, Cloudinary, Brevo, AI, OpenRouter, Meta hoặc phiên
  bản API tích hợp.
- Thay connection string, secret, token, ID tài khoản hệ thống hoặc cấu hình production.
- Chạy migration/database update trên database không được xác định rõ là môi trường
  được phép thay đổi.
- Gọi API bên ngoài để gửi email, tin nhắn, thanh toán hoặc thông báo thật.
- Reformat hoặc refactor diện rộng ngoài phạm vi yêu cầu.
- Hoàn tác thay đổi đang có trong worktree mà agent không tạo ra.

Nếu yêu cầu đụng tới một mục ở trên, agent phải nêu ảnh hưởng, phương án rollback
và xin xác nhận trước khi thực hiện hành động không thể phục hồi hoặc gây breaking change.

## Quy trình làm việc

1. Đọc `git status`, file liên quan và test hiện có trước khi sửa.
2. Xác định contract, dữ liệu và luồng người dùng bị ảnh hưởng.
3. Sửa phạm vi nhỏ nhất có thể và giữ kiến trúc hiện tại.
4. Với schema change, tạo migration mới và kiểm tra pending model changes.
5. Chạy test liên quan, sau đó build solution. Không che giấu warning hoặc test chưa chạy.
6. Kiểm tra diff để không đưa secret, file build hoặc thay đổi ngoài phạm vi vào bàn giao.

## Cách báo cáo sau mỗi lần sửa

Báo cáo cuối phải ngắn gọn nhưng có đủ các mục sau:

- Kết quả: hành vi nào đã thay đổi theo góc nhìn người dùng hoặc API consumer.
- File chính: liệt kê file quan trọng đã sửa và lý do.
- Dữ liệu/API: nêu migration, field, route, config hoặc breaking change nếu có.
- Kiểm tra: ghi đúng lệnh đã chạy và kết quả pass/fail, gồm số lượng test khi có.
- Việc còn lại: migration chưa apply, biến môi trường cần cấu hình, deploy hoặc kiểm
  thử thủ công chưa thực hiện.
- Rủi ro: warning có sẵn, giới hạn kỹ thuật hoặc giả định cần người dùng biết.

Không báo “hoàn tất” nếu test bắt buộc chưa chạy, migration chưa được tạo, hoặc còn
lỗi đã biết ảnh hưởng trực tiếp đến yêu cầu.
