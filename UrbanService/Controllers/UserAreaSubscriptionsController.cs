using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SERVICEUSER)]
[Route("api/user/area-subscriptions")]
public class UserAreaSubscriptionsController : ControllerBase
{
    private readonly IAreaAlertService _areaAlertService;

    public UserAreaSubscriptionsController(IAreaAlertService areaAlertService)
    {
        _areaAlertService = areaAlertService;
    }

    /// <summary>User xem danh sách khu vực đã đăng ký nhận cảnh báo.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<UserAreaSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMySubscriptions()
    {
        var result = await _areaAlertService.GetMySubscriptionsAsync(GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>User đăng ký khu vực nhận cảnh báo.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserAreaSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateAreaSubscriptionRequest request)
    {
        var result = await _areaAlertService.CreateSubscriptionAsync(GetCurrentUserId(), request);
        return Ok(result);
    }

    /// <summary>User hủy đăng ký khu vực nhận cảnh báo.</summary>
    [HttpDelete("{areaId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteSubscription(int areaId)
    {
        await _areaAlertService.DeleteSubscriptionAsync(GetCurrentUserId(), areaId);
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