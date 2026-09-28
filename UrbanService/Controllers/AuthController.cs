using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using UrbanService.BLL.Common;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using UrbanService.RateLimiting;

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
        [EnableRateLimiting(RateLimitPolicyNames.AuthAttempt)]
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
        /// Tài khoản chưa xác thực số điện thoại cũng trả `200` và vẫn có token,
        /// nhưng body là `UnverifiedLoginResultDto` với `code = PHONE_NOT_VERIFIED`.
        /// Token đó đọc được dữ liệu bình thường nhưng bị từ chối ở mọi thao tác ghi,
        /// trừ các API hoàn tất đăng ký.
        /// </response>
        /// <response code="400">Email hoặc mật khẩu không hợp lệ.</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.AuthAttempt)]
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
        [EnableRateLimiting(RateLimitPolicyNames.AuthAttempt)]
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
        [EnableRateLimiting(RateLimitPolicyNames.Otp)]
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
        [EnableRateLimiting(RateLimitPolicyNames.Otp)]
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
        [EnableRateLimiting(RateLimitPolicyNames.Otp)]
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
        [AllowUnverifiedPhone]
        [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
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

        /// <summary>Xin phép gửi SMS OTP xác thực số điện thoại.</summary>
        /// <remarks>
        /// Yêu cầu JWT hợp lệ. Backend **không** gửi SMS — Firebase gửi từ phía client.
        /// Endpoint này chỉ kiểm tra điều kiện rồi ghi nhận một lượt: số hợp lệ, chưa
        /// thuộc tài khoản đã xác thực khác, và hôm nay hệ thống còn hạn mức SMS.
        ///
        /// Client chỉ được gọi `signInWithPhoneNumber` của Firebase **sau khi** endpoint
        /// này trả `200`. Mỗi tin SMS là chi phí thật nên hạn mức nằm ở backend; chặn ở
        /// frontend thì người gọi thẳng Firebase bằng API key công khai vẫn đốt tiền.
        ///
        /// Bỏ trống `phoneNumber` để dùng số đã lưu trên tài khoản. Gửi số khác thì số
        /// của tài khoản được cập nhật luôn, phục vụ trường hợp gõ nhầm lúc đăng ký.
        ///
        /// Số khai trong `PhoneOtp:TestNumbers` không tốn SMS nên không tính lượt và
        /// trả `remainingToday = null`.
        /// </remarks>
        /// <response code="200">Được phép gửi OTP.</response>
        /// <response code="409">Số điện thoại đã thuộc về tài khoản đã xác thực khác.</response>
        /// <response code="429">Hết hạn mức SMS trong ngày.</response>
        [HttpPost("phone-verification/request-otp")]
        [Authorize]
        [AllowUnverifiedPhone]
        [ProducesResponseType(typeof(RequestPhoneOtpResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> RequestPhoneOtp(
            [FromBody] RequestPhoneOtpRequest req,
            CancellationToken cancellationToken)
            => Ok(await _auth.RequestPhoneOtpAsync(GetCurrentUserId(), req, cancellationToken));

        /// <summary>Xác thực số điện thoại bằng Firebase ID token.</summary>
        /// <remarks>
        /// Yêu cầu JWT hợp lệ. Client gửi lên `idToken` nhận được từ Firebase sau khi
        /// người dùng nhập đúng OTP.
        ///
        /// Số điện thoại được lấy từ token đã qua kiểm tra chữ ký của Firebase Admin
        /// SDK, không lấy từ request — nếu không thì client chỉ cần gửi đại một số là
        /// qua cửa. Token chỉ dùng được trong vòng `Firebase:MaxAuthAgeMinutes` phút
        /// kể từ lúc nhập OTP.
        ///
        /// Thành công thì `isVerified = true` và response trả JWT mới, vì trạng thái
        /// xác thực nằm trong claim của token.
        /// </remarks>
        /// <response code="200">Xác thực thành công, trả JWT mới.</response>
        /// <response code="400">Token không hợp lệ hoặc đã quá hạn.</response>
        /// <response code="409">Số điện thoại đã thuộc về tài khoản đã xác thực khác.</response>
        [HttpPost("phone-verification/verify")]
        [Authorize]
        [AllowUnverifiedPhone]
        [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> VerifyPhone(
            [FromBody] VerifyPhoneRequest req,
            CancellationToken cancellationToken)
            => Ok(await _auth.VerifyPhoneAsync(GetCurrentUserId(), req, cancellationToken));

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
