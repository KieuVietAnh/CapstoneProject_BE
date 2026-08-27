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
    private readonly IAreaAlertService _areaAlertService;
    private readonly IFeedbackDuplicateCandidateService _feedbackDuplicateCandidateService;

    public ManagementFeedbacksController(
        IFeedbackService feedbackService,
        IAreaAlertService areaAlertService,
        IFeedbackDuplicateCandidateService feedbackDuplicateCandidateService)
    {
        _feedbackService = feedbackService;
        _areaAlertService = areaAlertService;
        _feedbackDuplicateCandidateService = feedbackDuplicateCandidateService;
    }

    /// <summary>Xem feedback theo phạm vi quản lý hoặc Incident được phân công.</summary>
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
        var result = await _feedbackService.GetAllFeedbacksAsync(GetCurrentUserId(), query);
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
        var result = await _feedbackService.GetAiReviewedFeedbacksAsync(GetCurrentUserId(), query);
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

    /// <summary>Xóa một feedback bất kỳ trong hệ thống.</summary>
    /// <remarks>Chỉ role `SYSTEMADMIN` được phép xóa phản ánh.</remarks>
    [HttpDelete("{feedbackId:guid}")]
    [Authorize(Roles = UserRole.SYSTEMADMIN)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteFeedback(Guid feedbackId)
    {
        await _feedbackService.DeleteByManagementAsync(feedbackId);
        return NoContent();
    }

    /// <summary>Lấy các phản ánh trùng đã được liên kết vào phản ánh chính.</summary>
    [HttpGet("{feedbackId:guid}/linked-feedbacks")]
    [ProducesResponseType(typeof(IReadOnlyCollection<FeedbackListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLinkedFeedbacks(Guid feedbackId)
    {
        await _feedbackService.EnsureManagementFeedbackReadAccessAsync(
            feedbackId,
            GetCurrentUserId());
        var result = await _feedbackDuplicateCandidateService.GetLinkedFeedbacksAsync(feedbackId);
        return Ok(result);
    }

    /// <summary>Lấy phản ánh chính và các phản ánh cùng được liên kết.</summary>
    [HttpGet("{feedbackId:guid}/related")]
    [ProducesResponseType(typeof(RelatedFeedbacksDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRelatedFeedbacks(Guid feedbackId)
    {
        await _feedbackService.EnsureManagementFeedbackReadAccessAsync(
            feedbackId,
            GetCurrentUserId());
        var result = await _feedbackDuplicateCandidateService.GetRelatedFeedbacksAsync(feedbackId);
        return Ok(result);
    }

    /// <summary>Lay danh sach Service Provider phu hop voi area/category cua feedback.</summary>
    [HttpGet("{feedbackId:guid}/provider-candidates")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProviderCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProviderCandidates(Guid feedbackId)
    {
        var result = await _feedbackService.GetProviderCandidatesAsync(
            feedbackId,
            GetCurrentUserId());
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
        var result = await _feedbackService.GetProviderReportsAsync(
            feedbackId,
            GetCurrentUserId());
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
        var result = await _feedbackService.GetFeedbackResolutionsAsync(
            feedbackId,
            GetCurrentUserId());
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
        var result = await _feedbackService.GetResolutionAsync(
            resolutionId,
            GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>Tao canh bao khu vuc tu feedback nghiem trong.</summary>
    /// <remarks>Chỉ Manager phụ trách phường của phản ánh được phép tạo.</remarks>
    [HttpPost("{feedbackId:guid}/area-alert")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(UrbanService.BLL.Dtos.UserAreaAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAreaAlertFromFeedback(
        Guid feedbackId,
        [FromBody] CreateAreaAlertFromFeedbackRequest request)
    {
        await _feedbackService.EnsureManagementFeedbackReadAccessAsync(
            feedbackId,
            GetCurrentUserId());
        var result = await _areaAlertService.CreateAlertFromFeedbackAsync(
            GetCurrentUserId(),
            feedbackId,
            request);

        return Ok(result);
    }

    /// <summary>Gui notification thu cong cho nguoi dan ve ket qua/provider status cua feedback.</summary>
    [HttpPost("{feedbackId:guid}/notify-provider-result")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> NotifyProviderResult(
        Guid feedbackId,
        [FromBody] NotifyProviderResultRequest request)
    {
        await _feedbackService.NotifyProviderResultAsync(
            feedbackId,
            GetCurrentUserId(),
            request);

        return Ok(new
        {
            Message = "Notification sent successfully."
        });
    }

    /// <summary>Manager chỉnh sửa dữ liệu phân loại của phản ánh trong phường phụ trách.</summary>
    /// <remarks>
    /// Chỉ Manager phụ trách phường được sửa category, priority và nội dung.
    /// Trạng thái không được thay đổi qua request này; phải dùng endpoint workflow riêng.
    /// </remarks>
    [HttpPut("{feedbackId:guid}")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateFeedback(Guid feedbackId, [FromBody] StaffFeedbackUpdateRequest request)
    {
        var result = await _feedbackService.UpdateByStaffAsync(GetCurrentUserId(), feedbackId, request);
        return Ok(result);
    }

    /// <summary>Manager cập nhật trạng thái kiểm duyệt feedback.</summary>
    /// <remarks>
    /// Chỉ Manager phụ trách phường được phép từ chối hoặc hủy phản ánh.
    ///
    /// Sau khi cập nhật thành công, hệ thống lưu notification và gửi realtime
    /// event `NotificationReceived` qua SignalR tới người tạo feedback.
    ///
    /// Status hợp lệ tại endpoint này: `Rejected`, `Cancelled`.
    /// Xác nhận dùng endpoint `verify`; trạng thái xử lý đi qua Incident/provider/approval.
    /// </remarks>
    [HttpPatch("{feedbackId:guid}/status")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
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
    /// Manager verify feedback
    /// </summary>
    [HttpPut("{feedbackId:guid}/verify")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
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
    /// Staff gửi kết quả xử lý
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
