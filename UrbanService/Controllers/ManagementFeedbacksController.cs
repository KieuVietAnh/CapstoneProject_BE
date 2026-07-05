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

    /// <summary>Xem danh sach feedback da duoc AI review.</summary>
    /// <remarks>Role duoc phep: SYSTEMADMIN, SYSTEMSTAFF, INTERACTIONMANAGER.</remarks>
    [HttpGet("ai-reviewed")]
    [ProducesResponseType(typeof(PagedResultDto<FeedbackWithAnalysisResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAiReviewedFeedbacks([FromQuery] FeedbackQueryParameters query)
    {
        var result = await _feedbackService.GetAiReviewedFeedbacksAsync(query);
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

    /// <summary>Lay danh sach Service Provider phu hop voi area/category cua feedback.</summary>
    [HttpGet("{feedbackId:guid}/provider-candidates")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProviderCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProviderCandidates(Guid feedbackId)
    {
        var result = await _feedbackService.GetProviderCandidatesAsync(feedbackId);
        return Ok(result);
    }

    /// <summary>Xem cac lan feedback da duoc report sang Service Provider.</summary>
    [HttpGet("{feedbackId:guid}/provider-reports")]
    [ProducesResponseType(typeof(IReadOnlyCollection<FeedbackProviderReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProviderReports(Guid feedbackId)
    {
        var result = await _feedbackService.GetProviderReportsAsync(feedbackId);
        return Ok(result);
    }

    /// <summary>Xem lich su ket qua xu ly staff da submit cho feedback.</summary>
    [HttpGet("{feedbackId:guid}/resolutions")]
    [ProducesResponseType(typeof(IReadOnlyCollection<FeedbackResolutionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFeedbackResolutions(Guid feedbackId)
    {
        var result = await _feedbackService.GetFeedbackResolutionsAsync(feedbackId);
        return Ok(result);
    }

    /// <summary>Xem chi tiet mot ket qua xu ly.</summary>
    [HttpGet("resolutions/{resolutionId:int}")]
    [ProducesResponseType(typeof(FeedbackResolutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetResolution(int resolutionId)
    {
        var result = await _feedbackService.GetResolutionAsync(resolutionId);
        return Ok(result);
    }

    /// <summary>Gui notification thu cong cho nguoi dan ve ket qua/provider status cua feedback.</summary>
    [HttpPost("{feedbackId:guid}/notify-provider-result")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF + "," + UserRole.SYSTEMADMIN)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> NotifyProviderResult(
        Guid feedbackId,
        [FromBody] NotifyProviderResultRequest request)
    {
        await _feedbackService.NotifyProviderResultAsync(feedbackId, request);

        return Ok(new
        {
            Message = "Notification sent successfully."
        });
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
    /// Status hợp lệ: `Submitted`, `AiReviewed`, `Verified`, `Assigned`, `InProgress`, `Resolved`,
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

    /// <summary>
    /// Staff verify feedback
    /// </summary>
    [HttpPut("{feedbackId:guid}/verify")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    public async Task<IActionResult> VerifyFeedback(
        Guid feedbackId)
    {
        await _feedbackService.VerifyFeedbackAsync(
            feedbackId,
            GetCurrentUserId());

        return Ok(new
        {
            Message = "Feedback verified successfully."
        });
    }

    /// <summary>
    /// Staff report feedback cho coordinator
    /// </summary>
    [HttpPost("assign")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    public async Task<IActionResult> AssignFeedback(
        [FromBody] AssignFeedbackRequest request)
    {
        request.StaffUserId =
            GetCurrentUserId();

        var result = await _feedbackService.AssignFeedbackAsync(
            request);

        return Ok(result);
    }

    /// <summary>
    /// Operator gửi kết quả xử lý
    /// </summary>
    [HttpPost("submit-resolution")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    public async Task<IActionResult> SubmitResolution(
        [FromBody] SubmitResolutionRequest request)
    {
        request.StaffUserId =
            GetCurrentUserId();

        await _feedbackService
            .SubmitResolutionAsync(request);

        return Ok(new
        {
            Message = "Resolution submitted successfully."
        });
    }

    /// <summary>
    /// Manager duyệt kết quả xử lý
    /// </summary>
    [HttpPut("{feedbackId:guid}/approve")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    public async Task<IActionResult> ApproveResolution(
        Guid feedbackId,
        [FromQuery] string? note)
    {
        await _feedbackService
            .ApproveResolutionAsync(
                feedbackId,
                GetCurrentUserId(),
                note);

        return Ok(new
        {
            Message = "Resolution approved successfully."
        });
    }

    [HttpPut("{feedbackId:guid}/need-rework")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    public async Task<IActionResult> NeedRework(
    Guid feedbackId,
    [FromBody] string reason)
    {
        await _feedbackService.RequireReworkAsync(
            feedbackId,
            GetCurrentUserId(),
            reason);

        return Ok(new
        {
            Message = "Feedback marked as NeedRework."
        });
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
