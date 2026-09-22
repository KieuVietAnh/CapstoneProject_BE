using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using UrbanService.BLL.Common;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.Authorization;

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
        [AllowAnonymous]
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
        /// <response code="200">
        /// Đăng nhập thành công, trả về JWT và thông tin người dùng.
        ///
        /// Tài khoản chưa xác thực email cũng trả `200` và vẫn có token, nhưng body
        /// là `UnverifiedLoginResultDto` với `code = EMAIL_NOT_VERIFIED`. Token đó
        /// đọc được dữ liệu bình thường nhưng bị từ chối ở mọi thao tác ghi, trừ các
        /// API hoàn tất đăng ký.
        /// </response>
        /// <response code="400">Email hoặc mật khẩu không hợp lệ.</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(UnverifiedLoginResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var result = await _auth.LoginAsync(req);

            if (result.IsVerified)
            {
                return Ok(result);
            }

            return Ok(new UnverifiedLoginResultDto
            {
                Token = result.Token,
                RefreshToken = result.RefreshToken,
                User = new UnverifiedLoginUserDto
                {
                    Id = result.UserId,
                    Email = result.Email,
                    FullName = result.FullName,
                    PhoneNumber = result.PhoneNumber,
                    IsVerified = result.IsVerified
                }
            });
        }

        /// <summary>Cấp access token mới bằng refresh token.</summary>
        /// <remarks>
        /// API công khai. Client gửi refresh token nhận từ login/register/google-login.
        /// Refresh token sẽ được rotate sau mỗi lần gọi thành công.
        /// </remarks>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
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
        [AllowAnonymous]
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

        /// <summary>Kiểm tra OTP đặt lại mật khẩu trước khi nhập mật khẩu mới.</summary>
        /// <remarks>
        /// API công khai. Dùng cho giao diện tách bước: sau khi nhận OTP, client gọi
        /// endpoint này để biết mã đúng hay sai trước khi hiện màn nhập mật khẩu mới.
        ///
        /// OTP **không** bị tiêu thụ ở đây, vẫn phải gửi lại trong
        /// `forgot-password/reset`. Nhập sai vẫn tính vào giới hạn số lần thử.
        /// </remarks>
        /// <response code="204">OTP hợp lệ.</response>
        /// <response code="400">OTP không hợp lệ hoặc đã hết hạn.</response>
        [HttpPost("forgot-password/verify-otp")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyForgotPasswordOtp(
            [FromBody] VerifyForgotPasswordOtpRequest req,
            CancellationToken cancellationToken)
        {
            await _auth.VerifyForgotPasswordOtpAsync(req, cancellationToken);
            return NoContent();
        }

        /// <summary>Đặt mật khẩu mới bằng OTP đã gửi qua email.</summary>
        /// <remarks>
        /// API công khai. OTP chỉ dùng một lần; mật khẩu mới phải có ít nhất 8 ký tự.
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

        /// <summary>Sửa thông tin đăng ký của tài khoản chưa xác thực email.</summary>
        /// <remarks>
        /// Yêu cầu JWT hợp lệ. Dùng khi người dùng gõ nhầm email lúc đăng ký: họ
        /// không nhận được OTP nên không tự xác thực được, mà đăng ký lại cũng
        /// không xong vì email cũ đã chiếm chỗ.
        ///
        /// Giữ nguyên email của chính tài khoản thì **không** báo trùng. Đổi sang
        /// email đang thuộc tài khoản khác thì trả `400`.
        ///
        /// Khi email đổi, OTP cũ bị hủy ngay và một OTP mới được gửi tới email mới
        /// trong cùng lời gọi này, nên client **không** cần gọi thêm
        /// `email-verification/send-otp`.
        ///
        /// Response trả JWT mới vì email nằm trong claim của token.
        /// </remarks>
        /// <response code="200">Cập nhật thành công, trả JWT mới.</response>
        /// <response code="400">Email không hợp lệ, đã được dùng, hoặc tài khoản đã xác thực.</response>
        [HttpPatch("pending-account")]
        [Authorize]
        [AllowUnverifiedEmail]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdatePendingAccount(
            [FromBody] PendingAccountUpdateRequest req,
            CancellationToken cancellationToken)
        {
            var result = await _auth.UpdatePendingAccountAsync(
                GetCurrentUserId(),
                req,
                cancellationToken);
            return Ok(result);
        }

        /// <summary>Gửi OTP xác thực email tới email của người dùng hiện tại.</summary>
        /// <remarks>
        /// Yêu cầu JWT hợp lệ. OTP có hiệu lực trong 5 phút. Brevo API phải được
        /// cấu hình trong section `Brevo`.
        /// </remarks>
        [HttpPost("email-verification/send-otp")]
        [Authorize]
        [AllowUnverifiedEmail]
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
        [AllowUnverifiedEmail]
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
