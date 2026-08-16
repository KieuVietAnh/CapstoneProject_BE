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
        Task RequestEmailVerificationOtpAsync(Guid userId);
        Task VerifyEmailAsync(Guid userId, VerifyEmailRequest req);
        Task RequestForgotPasswordOtpAsync(
            ForgotPasswordRequest req,
            CancellationToken cancellationToken = default);
        Task ResetPasswordAsync(
            ResetPasswordRequest req,
            CancellationToken cancellationToken = default);
    }
}
