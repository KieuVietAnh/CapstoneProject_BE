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

    public class VerifyEmailRequest
    {
        public string Otp { get; set; } = "";
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
        public bool IsVerified { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = "";
    }

    // --- CẬP NHẬT CHO FORGET PASSWORD (XỬ LÝ TRÙNG EMAIL) ---

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = "";
        // Thêm Username để xác định chính xác tài khoản (vì 1 email có thể có nhiều account)
        public string Username { get; set; } = "";

    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = "";
        public string Otp { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }
}
