using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanService.DAL.Migrations
{
    /// <summary>
    /// Xoá các lượt gửi OTP đã bị ghi nhầm cho số điện thoại thử nghiệm.
    ///
    /// Số khai trong mục "Phone numbers for testing" của Firebase Console dùng mã cố
    /// định và Firebase không phát SMS thật cho chúng, nên chúng không tốn tiền và
    /// không được tính vào hạn mức mỗi ngày. Trên môi trường thật, danh sách số test
    /// không nạp được nên mọi lần kiểm thử đều bị trừ vào hạn mức, và hạn mức trong
    /// ngày cạn sạch dù chưa có tin nhắn thật nào được gửi.
    ///
    /// Những hàng này lẽ ra không bao giờ nên tồn tại, nên xoá hẳn thay vì chỉ trừ
    /// lùi bộ đếm. Bảng phone_otp_requests chỉ là sổ ghi hạn mức, không phải dữ liệu
    /// nghiệp vụ, nên xoá không mất gì cần truy vết.
    ///
    /// Danh sách số viết thẳng ở đây vì migration không đọc được cấu hình ứng dụng.
    /// Đây là một lần dọn dẹp cho dữ liệu đã lỡ ghi sai; từ nay việc nhận diện số test
    /// do PhoneOtp:TestNumbersCsv lo, khai qua biến môi trường nên luôn nạp được.
    /// </summary>
    public partial class ClearOtpQuotaForTestNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM phone_otp_requests
                WHERE phone_number IN (
                    '+84909999999',
                    '+84987654321',
                    '+84901234567',
                    '+84999999999'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            /*
             * Không dựng lại các hàng đã xoá. Chúng là bản ghi sai, và dựng lại chỉ
             * làm hạn mức cạn đi một cách vô cớ.
             */
        }
    }
}
