using UrbanService.BLL.Common.Constraint;

namespace UrbanService.BLL.Dtos
{
    public class RegisterRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Fullname { get; set; }
        public string? Phone { get; set; }
    }

    public class VerifyOtpRequest
    {
        public string Email { get; set; } = "";
        public string Otp { get; set; } = "";
    }

    public class RequestPhoneOtpRequest
    {
        /// <summary>Bỏ trống để dùng số đã lưu trên tài khoản.</summary>
        public string? PhoneNumber { get; set; }
    }

    public class RequestPhoneOtpResultDto
    {
        public string PhoneNumber { get; set; } = "";

        /// <summary>Số lượt SMS còn lại trong ngày; null khi là số test nên không tính lượt.</summary>
        public int? RemainingToday { get; set; }

        public bool IsTestNumber { get; set; }
    }

    public class VerifyPhoneRequest
    {
        /// <summary>Firebase ID token nhận được sau khi người dùng nhập đúng OTP.</summary>
        public string IdToken { get; set; } = "";
    }

    public class PendingAccountUpdateRequest
    {
        public string? FullName { get; set; }
        public string Email { get; set; } = "";
        public string? PhoneNumber { get; set; }

        /// <summary>Bỏ trống nếu người dùng không đổi mật khẩu.</summary>
        public string? NewPassword { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class GoogleLoginRequest
    {
        /// <summary>Google ID token nhận từ Google Identity Services ở frontend.</summary>
        public string IdToken { get; set; } = "";
    }

    public class AuthResultDto
    {
        public string Token { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public Guid UserId { get; set; }
        public string Email { get; set; } = "";
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsVerified { get; set; }
    }

    /// <summary>
    /// Response của đăng nhập khi tài khoản chưa xác thực số điện thoại.
    ///
    /// Vẫn là 200 và vẫn cấp token, nhưng có <see cref="Code"/> để client rẽ thẳng
    /// sang màn xác thực thay vì phải tự suy ra từ cờ isVerified. Token đó đọc dữ
    /// liệu bình thường, chỉ bị chặn ở thao tác ghi.
    /// </summary>
    public class UnverifiedLoginResultDto
    {
        public string Code { get; set; } = BusinessErrorCode.PhoneNotVerified;
        public string Message { get; set; } = "Số điện thoại chưa được xác thực.";
        public string Token { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public UnverifiedLoginUserDto User { get; set; } = new();
    }

    public class UnverifiedLoginUserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = "";
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsVerified { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = "";
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = "";
    }

    public class VerifyForgotPasswordOtpRequest
    {
        public string Email { get; set; } = "";
        public string Otp { get; set; } = "";
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = "";
        public string Otp { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }
}
