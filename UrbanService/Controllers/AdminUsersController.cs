using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMADMIN)]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;

    public AdminUsersController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    /// <summary>Lấy danh sách tài khoản trong hệ thống.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được truy cập; hỗ trợ bộ lọc và phân trang từ query.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers([FromQuery] UserQueryParameters query)
    {
        var result = await _userManagementService.GetUsersAsync(query);
        return Ok(result);
    }

    /// <summary>Lấy danh sách role có thể dùng khi quản trị tài khoản.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được truy cập.</remarks>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyCollection<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoles()
    {
        var result = await _userManagementService.GetRolesAsync();
        return Ok(result);
    }

    /// <summary>Lấy chi tiết một tài khoản theo định danh.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được truy cập.</remarks>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUser(Guid userId)
    {
        var result = await _userManagementService.GetUserAsync(userId);
        return Ok(result);
    }

    /// <summary>Tạo tài khoản mới từ màn hình quản trị.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được thao tác; email và dữ liệu định danh phải hợp lệ.</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest request)
    {
        var result = await _userManagementService.CreateUserAsync(request);
        return Ok(result);
    }

    /// <summary>Cập nhật thông tin và role của một tài khoản.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được thao tác; áp dụng các ràng buộc tự bảo vệ tài khoản quản trị.</remarks>
    [HttpPut("{userId:guid}")]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] AdminUpdateUserRequest request)
    {
        var result = await _userManagementService.UpdateUserAsync(GetCurrentUserId(), userId, request);
        return Ok(result);
    }

    /// <summary>Kích hoạt hoặc khóa một tài khoản.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được thao tác; quản trị viên hiện tại không thể tự khóa chính mình.</remarks>
    [HttpPatch("{userId:guid}/active")]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetActive(Guid userId, [FromBody] AdminSetUserActiveRequest request)
    {
        var result = await _userManagementService.SetActiveAsync(
            GetCurrentUserId(),
            userId,
            request.IsActive);

        return Ok(result);
    }

    /// <summary>Đặt lại mật khẩu cho một tài khoản.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được thao tác. Thành công trả về `204 No Content`.</remarks>
    [HttpPatch("{userId:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResetPassword(
        Guid userId,
        [FromBody] AdminResetUserPasswordRequest request)
    {
        await _userManagementService.ResetPasswordAsync(userId, request);
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
