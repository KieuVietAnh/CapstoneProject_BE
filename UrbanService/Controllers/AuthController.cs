using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using UrbanService.BLL.Common;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        /// <summary>Đăng ký tài khoản người dùng mới.</summary>
        /// <remarks>
        /// API công khai, không yêu cầu JWT. Role mặc định được lấy từ cấu hình
        /// `Auth:DefaultRole`, thông thường là `SERVICEUSER`.
        ///
        /// `phone` là bắt buộc và được chuẩn hóa về E.164. Endpoint này **không**
        /// trả về token: hệ thống gửi OTP qua SMS, client phải gọi
        /// `phone-verification/verify` để nhận token.
        ///
        /// Nếu email đã tồn tại nhưng chưa xác thực thì thông tin được cập nhật
        /// và OTP được gửi lại, thay vì báo trùng email.
        /// </remarks>
        /// <response code="200">Đã tạo tài khoản và gửi OTP. Chưa cấp token.</response>
        /// <response code="400">Dữ liệu không hợp lệ, tài khoản đã tồn tại hoặc không gửi được SMS.</response>
        [HttpPost("register")]
        [ProducesResponseType(typeof(RegisterResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest req,
            CancellationToken cancellationToken)
        {
            var result = await _auth.RegisterAsync(req, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Đăng nhập để lấy JWT dùng cho các API yêu cầu xác thực.
        /// </summary>
        /// <remarks>
        /// Sau khi đăng nhập, sao chép giá trị `token` trong response, bấm nút
        /// **Authorize** trên Swagger và nhập token. API tạo feedback yêu cầu tài
        /// khoản có role `SERVICEUSER`.
        /// </remarks>
        /// <response code="200">Đăng nhập thành công, trả về JWT và thông tin người dùng.</response>
        /// <response code="400">Email hoặc mật khẩu không hợp lệ.</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var result = await _auth.LoginAsync(req);
            return Ok(result);
        }

        /// <summary>Cấp access token mới bằng refresh token.</summary>
        /// <remarks>
        /// API công khai. Client gửi refresh token nhận từ login/register/google-login.
        /// Refresh token sẽ được rotate sau mỗi lần gọi thành công.
        /// </remarks>
        [HttpPost("refresh-token")]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest req)
        {
            var result = await _auth.RefreshTokenAsync(req);
            return Ok(result);
        }

        /// <summary>Đăng nhập bằng tài khoản Google đã xác thực.</summary>
        /// <remarks>
        /// Frontend gửi Google ID token nhận từ Google Identity Services.
        /// Backend xác minh token và chỉ đăng nhập khi email đã tồn tại trong
        /// UrbanService, `isVerified = true` và tài khoản đang hoạt động.
        ///
        /// API không tự động tạo tài khoản mới.
        /// </remarks>
        [HttpPost("google-login")]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req)
        {
            var result = await _auth.GoogleLoginAsync(req);
            return Ok(result);
        }

        /// <summary>Gửi OTP đặt lại mật khẩu tới email tài khoản.</summary>
        /// <remarks>
        /// API công khai. Luôn trả về 204 cho request hợp lệ về định dạng, kể cả khi
        /// email không tồn tại, tài khoản bị khóa hoặc đang trong thời gian chờ gửi lại.
        /// OTP có hiệu lực trong 5 phút.
        /// </remarks>
        [HttpPost("forgot-password/send-otp")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendForgotPasswordOtp(
            [FromBody] ForgotPasswordRequest req,
            CancellationToken cancellationToken)
        {
            await _auth.RequestForgotPasswordOtpAsync(req, cancellationToken);
            return NoContent();
        }

        /// <summary>Đặt mật khẩu mới bằng OTP đã gửi qua email.</summary>
        /// <remarks>
        /// API công khai. OTP chỉ dùng một lần; mật khẩu mới phải có ít nhất 6 ký tự.
        /// Reset thành công sẽ thu hồi refresh token hiện tại của tài khoản.
        /// </remarks>
        [HttpPost("forgot-password/reset")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetForgottenPassword(
            [FromBody] ResetPasswordRequest req,
            CancellationToken cancellationToken)
        {
            await _auth.ResetPasswordAsync(req, cancellationToken);
            return NoContent();
        }

        /// <summary>Gửi lại OTP xác thực số điện thoại.</summary>
        /// <remarks>
        /// API công khai, không yêu cầu JWT, vì tài khoản chưa xác thực thì chưa
        /// có token. Có cooldown giữa hai lần gửi.
        ///
        /// Để tránh dò xem số nào đã đăng ký, endpoint luôn trả `204` kể cả khi
        /// số không tồn tại hoặc tài khoản đã xác thực.
        ///
        /// Yêu cầu cấu hình section `Twilio`.
        /// </remarks>
        [HttpPost("phone-verification/send-otp")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendPhoneVerificationOtp(
            [FromBody] SendPhoneOtpRequest req,
            CancellationToken cancellationToken)
        {
            await _auth.RequestPhoneVerificationOtpAsync(req, cancellationToken);
            return NoContent();
        }

        /// <summary>Xác thực số điện thoại bằng OTP và nhận token.</summary>
        /// <remarks>
        /// API công khai, không yêu cầu JWT. OTP đúng thì `isVerified` được đặt
        /// thành `true` và response trả về JWT cùng refresh token, hoàn tất
        /// đăng ký mà không cần gọi thêm `login`.
        ///
        /// Nhập sai quá số lần cho phép thì OTP bị hủy, phải yêu cầu gửi lại.
        /// </remarks>
        /// <response code="200">Xác thực thành công, trả về JWT và thông tin tài khoản.</response>
        /// <response code="400">OTP không đúng hoặc đã hết hạn.</response>
        [HttpPost("phone-verification/verify")]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyPhone(
            [FromBody] VerifyPhoneRequest req,
            CancellationToken cancellationToken)
        {
            var result = await _auth.VerifyPhoneAsync(req, cancellationToken);
            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                throw new UnauthorizedAccessException();
            }

            return parsedUserId;
        }
    }
}
