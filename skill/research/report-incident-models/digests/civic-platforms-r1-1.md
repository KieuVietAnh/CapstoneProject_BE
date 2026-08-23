# Digest: Civic reporting platforms

## FixMyStreet / FixMyStreet Pro

- Người dân thấy các report hiện có trên bản đồ; hệ thống gợi ý report gần đó cùng category. Nếu trùng, người dân được khuyến khích subscribe report cũ và không tạo report mới. Source: https://fixmystreet.org/pro-manual/print/ | publisher=mySociety/SocietyWorks | pub_date=n.d. | accessed=2026-08-23 | confidence=high | class=official-product-doc/direct
- Staff authority/admin có thể đóng report với trạng thái `duplicate`. Source: https://fixmystreet.org/running/admin_manual/ | publisher=mySociety | pub_date=n.d. | accessed=2026-08-23 | confidence=high | class=official-admin-doc/direct
- Core schema dùng `problem` cho một report, `comment.problem_id` cho update và `alert.parameter` làm Problem ID để subscribe; không có issue/case riêng hoặc FK `duplicate_of`. Source: https://raw.githubusercontent.com/mysociety/fixmystreet/master/db/schema.sql | publisher=mySociety | pub_date=n.d. | accessed=2026-08-23 | confidence=high | class=official-source-code/direct
- Inference: core model chủ yếu tránh report trùng; report đã tạo vẫn là `problem` riêng mang trạng thái duplicate, không có canonical Incident entity công khai. Confidence=medium-high.

## SeeClickFix / CivicPlus 311 CRM

- Public API coi mỗi submission là `issue`; authenticated user có thể follow và nhận update qua email. Source: https://dev.seeclickfix.com/v2/issues/follow/ | publisher=SeeClickFix/CivicPlus | pub_date=n.d. | accessed=2026-08-23 | confidence=high | class=official-api-doc/direct
- Status ownership chia sẻ theo quyền; status change phải kèm comment. Source: https://dev.seeclickfix.com/v2/issues/changing_status/ | publisher=SeeClickFix/CivicPlus | pub_date=n.d. | accessed=2026-08-23 | confidence=high | class=official-api-doc/direct
- CivicPlus công bố CRM tự route/assign theo location/category và nhận diện, merge duplicate submissions. Source: https://www.civicplus.com/seeclickfix-311-crm/unified-platform/ | publisher=CivicPlus | pub_date=n.d. | accessed=2026-08-23 | confidence=high cho capability, medium cho internal model | class=official-product-doc/direct
- Production record tại Tacoma cho thấy request `21403197` được add vào open case, đánh dấu duplicate của `21050046`; notification follower chuyển sang canonical issue. Source: https://seeclickfix.com/web_portal/Mx4UcnjshtJ83uMYFA2D58p5/issues/map/21403197 | publisher=City of Tacoma/SeeClickFix | pub_date=2026-04-06, action 2026-04-21 | accessed=2026-08-23 | confidence=high | class=production-record/direct
- Inference: hành vi vận hành là nhiều request quy về canonical issue/case; duplicate giữ ID riêng và notification chuyển sang canonical. Public docs chưa chứng minh có case entity riêng. Confidence=medium-high.

## Snap Send Solve

- Khi bật de-duplication, app tìm tối đa ba candidate; chọn candidate sẽ ghi `deflection` lên matched report và không tạo report mới. Nếu report gốc public, người dùng có thể lưu để theo dõi. Source: https://help.snapsendsolve.com/en/articles/11560117-how-are-duplicate-reports-handled | publisher=Snap Send Solve | pub_date=2025-06-12 | accessed=2026-08-23 | confidence=high | class=official-help-doc/direct
- Inference: mô hình công khai là một report nhận deflection/follower; submission thứ hai không trở thành persisted report. Confidence=high.

## Limitations và leads

- Không tìm thấy tài liệu chính thức chứng minh split trong ba hệ thống.
- CivicPlus không công khai schema/API merge nên chưa biết merge vật lý, soft-link hay case layer.
- Deployment FixMyStreet riêng có thể lưu liên kết qua extension/backend ngoài core schema.
- Bằng chứng mạnh nhất cho hành vi many-reports-one-canonical-case là CivicPlus/SeeClickFix; FixMyStreet và Snap Send Solve chủ yếu ngăn report mới.
