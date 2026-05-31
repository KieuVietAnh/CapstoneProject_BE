using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces
{
    public interface IAuthService
    {
        //Task RequestRegisterOtpAsync(RegisterRequest req);
        //Task<AuthResultDto> VerifyRegisterOtpAsync(VerifyOtpRequest req);
        Task<AuthResultDto> RegisterAsync(RegisterRequest req);
        Task<AuthResultDto> LoginAsync(LoginRequest req);

        // --- 2 PHƯƠNG THỨC MỚI CHO FORGET PASSWORD ---

        // 1. Gửi OTP quên mật khẩu vào Email
        //Task RequestForgotPasswordOtpAsync(ForgotPasswordRequest req);

        // 2. Xác nhận OTP từ Redis và đổi mật khẩu mới
        //Task ResetPasswordAsync(ResetPasswordRequest req);
    }
}