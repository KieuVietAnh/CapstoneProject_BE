using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SERVICEUSER)]
[Route("api/profile")]
public class UserProfileController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;

    public UserProfileController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    /// <summary>Lấy hồ sơ của người dùng hiện tại.</summary>
    /// <remarks>Chỉ `SERVICEUSER`; định danh người dùng được lấy từ JWT.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _userManagementService.GetMyProfileAsync(GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>Cập nhật hồ sơ của người dùng hiện tại.</summary>
    /// <remarks>Chỉ `SERVICEUSER`; chỉ các trường hồ sơ được phép mới được cập nhật.</remarks>
    [HttpPut]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateUserProfileRequest request)
    {
        var result = await _userManagementService.UpdateMyProfileAsync(GetCurrentUserId(), request);
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
