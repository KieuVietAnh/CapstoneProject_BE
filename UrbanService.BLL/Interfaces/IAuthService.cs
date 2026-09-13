using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// Tạo tài khoản và gửi OTP xác thực số điện thoại qua SMS.
        ///
        /// Trả về token ngay: đăng nhập không đòi tài khoản đã xác thực. Client
        /// đọc <c>IsVerified</c> để biết có cần nhắc người dùng xác thực OTP hay
        /// không; chưa xác thực thì vẫn dùng được hệ thống nhưng không gửi được
        /// phản ánh.
        /// </summary>
        Task<AuthResultDto> RegisterAsync(
            RegisterRequest req,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bổ sung số điện thoại cho tài khoản đã đăng nhập nhưng chưa xác thực,
        /// rồi gửi OTP. Dùng cho người đăng nhập bằng Google.
        /// </summary>
        Task AttachPhoneAsync(
            Guid userId,
            SendPhoneOtpRequest req,
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
