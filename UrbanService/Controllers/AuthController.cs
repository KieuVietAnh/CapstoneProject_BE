using Microsoft.AspNetCore.Mvc;
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
    }
}
