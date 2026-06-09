using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/management/feedbacks")]
public class ManagementFeedbacksController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;

    public ManagementFeedbacksController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    /// <summary>Xem danh sách tất cả feedback trong hệ thống.</summary>
    /// <remarks>
    /// Role được phép: `SYSTEMADMIN`, `SYSTEMSTAFF`, `INTERACTIONMANAGER`.
    /// Hỗ trợ phân trang và lọc theo `status`, `categoryId`, `search`.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<FeedbackListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllFeedbacks([FromQuery] FeedbackQueryParameters query)
    {
        var result = await _feedbackService.GetAllFeedbacksAsync(query);
        return Ok(result);
    }

    /// <summary>Xem chi tiết một feedback bất kỳ.</summary>
    /// <remarks>Role được phép: `SYSTEMADMIN`, `SYSTEMSTAFF`, `INTERACTIONMANAGER`.</remarks>
    [HttpGet("{feedbackId:guid}")]
    [ProducesResponseType(typeof(FeedbackDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFeedbackDetail(Guid feedbackId)
    {
        var result = await _feedbackService.GetFeedbackDetailAsync(GetCurrentUserId(), feedbackId);
        return Ok(result);
    }

    /// <summary>Staff chỉnh sửa feedback của người dân.</summary>
    /// <remarks>
    /// Chỉ role `SYSTEMSTAFF` được phép sử dụng. Có thể sửa category, priority,
    /// nội dung và status. Nếu `status` thay đổi, người tạo feedback sẽ nhận
    /// notification và event SignalR `NotificationReceived`.
    /// </remarks>
    [HttpPut("{feedbackId:guid}")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(FeedbackDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateFeedback(Guid feedbackId, [FromBody] StaffFeedbackUpdateRequest request)
    {
        var result = await _feedbackService.UpdateByStaffAsync(GetCurrentUserId(), feedbackId, request);
        return Ok(result);
    }

    /// <summary>Staff hoặc Admin cập nhật trạng thái feedback.</summary>
    /// <remarks>
    /// Role được phép: `SYSTEMSTAFF`, `SYSTEMADMIN`.
    ///
    /// Sau khi cập nhật thành công, hệ thống lưu notification và gửi realtime
    /// event `NotificationReceived` qua SignalR tới người tạo feedback.
    ///
    /// Status hợp lệ: `Submitted`, `Verified`, `Assigned`, `InProgress`, `Resolved`,
    /// `SubmittedForApproval`, `Approved`, `Rejected`, `NeedRework`, `Closed`,
    /// `Cancelled`.
    /// </remarks>
    [HttpPatch("{feedbackId:guid}/status")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF + "," + UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(FeedbackStatusHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus(Guid feedbackId, [FromBody] UpdateFeedbackStatusRequest request)
    {
        var result = await _feedbackService.UpdateStatusByStaffOrAdminAsync(
            GetCurrentUserId(),
            feedbackId,
            request);
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
