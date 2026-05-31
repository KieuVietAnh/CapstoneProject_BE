namespace UrbanService.BLL.Dtos
{
    public class RegisterRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Fullname { get; set; }
        public string? Phone { get; set; }
    }

    public class VerifyOtpRequest
    {
        public string Username { get; set; } = "";
        public string Otp { get; set; } = "";
    }

    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class AuthResultDto
    {
        public string Token { get; set; } = "";
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
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
        public string Username { get; set; } = ""; // Thêm Username để định danh chính xác khi update DB
        public string Otp { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }
}
