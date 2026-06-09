using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Xem danh sách notification của tài khoản đang đăng nhập.</summary>
    /// <remarks>
    /// Yêu cầu JWT hợp lệ. Hỗ trợ phân trang và lọc trạng thái đọc bằng `isRead`.
    /// Notification realtime được nhận qua SignalR hub `/hubs/notifications`,
    /// event `NotificationReceived`.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isRead = null)
    {
        var result = await _notificationService.GetMyNotificationsAsync(
            GetCurrentUserId(),
            pageNumber,
            pageSize,
            isRead);
        return Ok(result);
    }

    /// <summary>Đánh dấu một notification là đã đọc.</summary>
    /// <remarks>Chỉ cập nhật notification thuộc tài khoản đang đăng nhập.</remarks>
    [HttpPatch("{notificationId:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(UrbanService.BLL.Common.ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(int notificationId)
    {
        await _notificationService.MarkAsReadAsync(GetCurrentUserId(), notificationId);
        return NoContent();
    }

    /// <summary>Đánh dấu tất cả notification của tài khoản là đã đọc.</summary>
    /// <remarks>Yêu cầu JWT hợp lệ và chỉ tác động đến notification của tài khoản hiện tại.</remarks>
    [HttpPatch("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync(GetCurrentUserId());
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
