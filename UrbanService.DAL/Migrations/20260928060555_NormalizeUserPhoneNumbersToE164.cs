using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <summary>
    /// Chuẩn hoá số điện thoại đang lưu về E.164.
    ///
    /// Dữ liệu cũ lẫn hai dạng: có hàng lưu "0438959112", có hàng lưu "+84999999999".
    /// Luồng xác thực mới luôn so khớp theo E.164 — số lấy từ token Firebase bao giờ
    /// cũng ở dạng đó — nên hàng nào còn dạng nội địa sẽ không bao giờ khớp: đăng
    /// nhập bằng số điện thoại không tìm ra tài khoản, và kiểm tra trùng số bỏ sót.
    ///
    /// Chỉ đụng tới hàng bắt đầu bằng "0" hoặc "84". Hàng đã có "+" giữ nguyên, hàng
    /// NULL giữ nguyên, và hàng có ký tự lạ cũng giữ nguyên để không tự ý bịa dữ liệu.
    /// </summary>
    public partial class NormalizeUserPhoneNumbersToE164 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "0438959112" -> "+84438959112"
            migrationBuilder.Sql(@"
                UPDATE users
                SET phone_number = '+84' || substring(phone_number from 2)
                WHERE phone_number IS NOT NULL
                  AND phone_number ~ '^0[0-9]{8,11}$';
            ");

            // "84987654321" -> "+84987654321"
            migrationBuilder.Sql(@"
                UPDATE users
                SET phone_number = '+' || phone_number
                WHERE phone_number IS NOT NULL
                  AND phone_number ~ '^84[0-9]{7,12}$';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            /*
             * Đưa ngược về dạng nội địa cho số Việt Nam. Không khôi phục được chính
             * xác trạng thái trước đó, vì sau khi chạy Up thì không còn phân biệt
             * được hàng nào vốn đã là E.164 và hàng nào vừa được đổi. Chấp nhận:
             * mục tiêu của Down chỉ là đưa cột về một dạng dùng được, không phải
             * dựng lại từng giá trị cũ.
             */
            migrationBuilder.Sql(@"
                UPDATE users
                SET phone_number = '0' || substring(phone_number from 4)
                WHERE phone_number IS NOT NULL
                  AND phone_number ~ '^\+84[0-9]{8,11}$';
            ");
        }
    }
}
