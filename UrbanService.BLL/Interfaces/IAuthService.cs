using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces
{
    public interface IAuthService
    {
        //Task RequestRegisterOtpAsync(RegisterRequest req);
        //Task<AuthResultDto> VerifyRegisterOtpAsync(VerifyOtpRequest req);
        Task<AuthResultDto> RegisterAsync(RegisterRequest req);
        Task<AuthResultDto> LoginAsync(LoginRequest req);
        Task<AuthResultDto> GoogleLoginAsync(GoogleLoginRequest req);
        Task<AuthResultDto> RefreshTokenAsync(RefreshTokenRequest req);
        /// <summary>Xin phép gửi SMS OTP; kiểm tra hạn mức trước khi client gọi Firebase.</summary>
        Task<RequestPhoneOtpResultDto> RequestPhoneOtpAsync(
            Guid userId,
            RequestPhoneOtpRequest req,
            CancellationToken cancellationToken = default);

        /// <summary>Xác thực Firebase ID token và đánh dấu tài khoản đã xác thực SĐT.</summary>
        Task<AuthResultDto> VerifyPhoneAsync(
            Guid userId,
            VerifyPhoneRequest req,
            CancellationToken cancellationToken = default);

        Task<AuthResultDto> UpdatePendingAccountAsync(
            Guid userId,
            PendingAccountUpdateRequest req,
            CancellationToken cancellationToken = default);
        Task RequestForgotPasswordOtpAsync(
            ForgotPasswordRequest req,
            CancellationToken cancellationToken = default);
        Task VerifyForgotPasswordOtpAsync(
            VerifyForgotPasswordOtpRequest req,
            CancellationToken cancellationToken = default);
        Task ResetPasswordAsync(
            ResetPasswordRequest req,
            CancellationToken cancellationToken = default);
    }
}
