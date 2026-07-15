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
        /// </remarks>
        /// <response code="200">Đăng ký thành công, trả về JWT và thông tin tài khoản.</response>
        /// <response code="400">Dữ liệu không hợp lệ hoặc tài khoản đã tồn tại.</response>
        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var result = await _auth.RegisterAsync(req);
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

        /// <summary>Gửi OTP xác thực email tới email của người dùng hiện tại.</summary>
        /// <remarks>
        /// Yêu cầu JWT hợp lệ. OTP có hiệu lực trong 5 phút. Brevo API phải được
        /// cấu hình trong section `Brevo`.
        /// </remarks>
        [HttpPost("email-verification/send-otp")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SendEmailVerificationOtp()
        {
            await _auth.RequestEmailVerificationOtpAsync(GetCurrentUserId());
            return NoContent();
        }

        /// <summary>Xác thực email bằng OTP.</summary>
        /// <remarks>
        /// Yêu cầu JWT hợp lệ. Sau khi OTP đúng, trường `isVerified` của người
        /// dùng được cập nhật thành `true`.
        /// </remarks>
        [HttpPost("email-verification/verify")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req)
        {
            await _auth.VerifyEmailAsync(GetCurrentUserId(), req);
            return NoContent();
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
