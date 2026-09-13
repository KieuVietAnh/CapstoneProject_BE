using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// Tạo tài khoản chưa xác thực và gửi OTP qua SMS.
        /// Không cấp token; token chỉ được cấp sau khi xác thực OTP.
        /// </summary>
        Task<RegisterResultDto> RegisterAsync(
            RegisterRequest req,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gửi lại OTP xác thực số điện thoại.
        /// </summary>
        Task RequestPhoneVerificationOtpAsync(
            SendPhoneOtpRequest req,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Xác thực OTP và cấp token cho tài khoản vừa xác thực.
        /// </summary>
        Task<AuthResultDto> VerifyPhoneAsync(
            VerifyPhoneRequest req,
            CancellationToken cancellationToken = default);

        Task<AuthResultDto> LoginAsync(LoginRequest req);
        Task<AuthResultDto> GoogleLoginAsync(GoogleLoginRequest req);
        Task<AuthResultDto> RefreshTokenAsync(RefreshTokenRequest req);
        Task RequestForgotPasswordOtpAsync(
            ForgotPasswordRequest req,
            CancellationToken cancellationToken = default);
        Task ResetPasswordAsync(
            ResetPasswordRequest req,
            CancellationToken cancellationToken = default);
    }
}
