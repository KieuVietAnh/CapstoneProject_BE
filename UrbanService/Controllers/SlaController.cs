using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UrbanService.BLL.Common;
using UrbanService.BLL.DTOs.SLA;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Route("api/slas")]
[Authorize]
public class SlaController : ControllerBase
{
    private readonly ISlaService _slaService;

    public SlaController(ISlaService slaService)
    {
        _slaService = slaService;
    }

    /// <summary>
    /// Lấy SLA hiện tại của một feedback.
    /// </summary>
    /// <remarks>
    /// FE gọi:
    ///
    ///     GET /api/slas/feedback/{feedbackId}
    ///
    /// Response gồm:
    ///
    /// - Deadline phản hồi.
    /// - Deadline hoàn thành.
    /// - Trạng thái SLA.
    /// - Trạng thái phản hồi và xử lý.
    /// - Số phút còn lại.
    /// - Lịch sử event.
    /// - Lịch sử pause/resume.
    /// </remarks>
    [HttpGet("feedback/{feedbackId:guid}")]
    [ProducesResponseType(
        typeof(ApiResponse<FeedbackSlaDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult>
        GetCurrentByFeedbackId(Guid feedbackId)
    {
        var result =
            await _slaService
                .GetCurrentByFeedbackIdAsync(feedbackId);

        return Ok(new ApiResponse<FeedbackSlaDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Lấy SLA của feedback thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Lấy chi tiết SLA theo FeedbackSlaId.
    /// </summary>
    [HttpGet("{feedbackSlaId:long}")]
    [ProducesResponseType(
        typeof(ApiResponse<FeedbackSlaDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        long feedbackSlaId)
    {
        var result =
            await _slaService.GetByIdAsync(feedbackSlaId);

        return Ok(new ApiResponse<FeedbackSlaDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Lấy chi tiết SLA thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Bắt đầu SLA thủ công cho feedback đã xác minh.
    /// </summary>
    /// <remarks>
    /// Trong luồng chính, SLA nên được gọi tự động sau khi staff
    /// xác minh feedback.
    ///
    /// Endpoint này dành cho Manager/Admin kiểm tra hoặc khôi phục
    /// trường hợp SLA chưa được tạo.
    ///
    ///     POST /api/slas/feedback/{feedbackId}/start
    /// </remarks>
    [HttpPost("feedback/{feedbackId:guid}/start")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(
        typeof(ApiResponse<FeedbackSlaDto>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(
        Guid feedbackId)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _slaService.StartAsync(
            feedbackId,
            currentUserId);

        return StatusCode(
            StatusCodes.Status201Created,
            new ApiResponse<FeedbackSlaDto>
            {
                Status = StatusCodes.Status201Created,
                Msg = "Bắt đầu SLA thành công.",
                Data = result
            });
    }

    /// <summary>
    /// Đánh dấu đã có phản hồi đầu tiên cho feedback.
    /// </summary>
    /// <remarks>
    /// FE gọi khi staff gửi phản hồi chính thức đầu tiên:
    ///
    ///     PATCH /api/slas/feedback/{feedbackId}/responded
    ///
    /// Body có thể gửi chuỗi ghi chú:
    ///
    ///     "Đã liên hệ và thông báo tiếp nhận cho người dân."
    /// </remarks>
    [HttpPatch("feedback/{feedbackId:guid}/responded")]
    [Authorize(Roles =
        "Admin,Manager,Staff,ServiceStaff")]
    [ProducesResponseType(
        typeof(ApiResponse<FeedbackSlaDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkResponded(
        Guid feedbackId,
        [FromBody] string? note)
    {
        var currentUserId = GetCurrentUserId();

        var result =
            await _slaService.MarkRespondedAsync(
                feedbackId,
                currentUserId,
                note);

        return Ok(new ApiResponse<FeedbackSlaDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Ghi nhận phản hồi đầu tiên thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Tạm dừng SLA.
    /// </summary>
    /// <remarks>
    /// ReasonCode nhận:
    ///
    /// - WaitingCitizen
    /// - ForceMajeure
    /// - ExternalDependency
    /// - SystemMaintenance
    /// - Other
    ///
    /// Ví dụ:
    ///
    ///     PATCH /api/slas/feedback/{feedbackId}/pause
    ///
    ///     {
    ///       "reasonCode": "WaitingCitizen",
    ///       "reasonNote": "Đang chờ người dân bổ sung hình ảnh."
    ///     }
    /// </remarks>
    [HttpPatch("feedback/{feedbackId:guid}/pause")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    [ProducesResponseType(
        typeof(ApiResponse<FeedbackSlaDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pause(
        Guid feedbackId,
        [FromBody] PauseSlaRequest request)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _slaService.PauseAsync(
            feedbackId,
            currentUserId,
            request);

        return Ok(new ApiResponse<FeedbackSlaDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Tạm dừng SLA thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Tiếp tục SLA đang tạm dừng.
    /// </summary>
    /// <remarks>
    /// Khi tiếp tục, thời gian đã pause được cộng vào cả:
    ///
    /// - ResponseDueAt.
    /// - ResolutionDueAt.
    ///
    /// Ví dụ:
    ///
    ///     PATCH /api/slas/feedback/{feedbackId}/resume
    ///
    ///     {
    ///       "note": "Người dân đã bổ sung đủ thông tin."
    ///     }
    /// </remarks>
    [HttpPatch("feedback/{feedbackId:guid}/resume")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    [ProducesResponseType(
        typeof(ApiResponse<FeedbackSlaDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Resume(
        Guid feedbackId,
        [FromBody] ResumeSlaRequest request)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _slaService.ResumeAsync(
            feedbackId,
            currentUserId,
            request);

        return Ok(new ApiResponse<FeedbackSlaDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Tiếp tục SLA thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Hoàn thành SLA.
    /// </summary>
    /// <remarks>
    /// FE gọi sau khi feedback đã được xử lý hoàn tất.
    ///
    ///     PATCH /api/slas/feedback/{feedbackId}/complete
    ///
    ///     {
    ///       "note": "Đã xử lý và xác nhận kết quả."
    ///     }
    /// </remarks>
    [HttpPatch("feedback/{feedbackId:guid}/complete")]
    [Authorize(Roles =
        "Admin,Manager,Staff,ServiceStaff")]
    [ProducesResponseType(
        typeof(ApiResponse<FeedbackSlaDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(
        Guid feedbackId,
        [FromBody] CompleteSlaRequest request)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _slaService.CompleteAsync(
            feedbackId,
            currentUserId,
            request);

        return Ok(new ApiResponse<FeedbackSlaDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Hoàn thành SLA thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Hủy SLA hiện tại.
    /// </summary>
    /// <remarks>
    /// Chỉ Admin hoặc Manager được phép hủy.
    ///
    /// Body có thể là chuỗi lý do:
    ///
    ///     "Feedback được xác định là trùng lặp."
    /// </remarks>
    [HttpPatch("feedback/{feedbackId:guid}/cancel")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(
        typeof(ApiResponse<FeedbackSlaDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        Guid feedbackId,
        [FromBody] string? note)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _slaService.CancelAsync(
            feedbackId,
            currentUserId,
            note);

        return Ok(new ApiResponse<FeedbackSlaDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Hủy SLA thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Chọn lại policy và tính lại deadline SLA.
    /// </summary>
    /// <remarks>
    /// Dùng khi category, priority hoặc khu vực của feedback thay đổi.
    ///
    ///     PATCH /api/slas/feedback/{feedbackId}/recalculate
    ///
    ///     {
    ///       "note": "Tính lại sau khi thay đổi priority."
    ///     }
    /// </remarks>
    [HttpPatch(
        "feedback/{feedbackId:guid}/recalculate")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(
        typeof(ApiResponse<FeedbackSlaDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Recalculate(
        Guid feedbackId,
        [FromBody] RecalculateSlaRequest request)
    {
        var currentUserId = GetCurrentUserId();

        var result =
            await _slaService.RecalculateAsync(
                feedbackId,
                currentUserId,
                request);

        return Ok(new ApiResponse<FeedbackSlaDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Tính lại SLA thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Kiểm tra vi phạm cho một SLA.
    /// </summary>
    /// <remarks>
    /// Chủ yếu dùng để kiểm thử hoặc chạy thủ công.
    /// Background worker sẽ tự động thực hiện việc này.
    /// </remarks>
    [HttpPost("{feedbackSlaId:long}/check-violation")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckViolation(
        long feedbackSlaId)
    {
        await _slaService.CheckViolationAsync(
            feedbackSlaId);

        return Ok(new ApiResponse<object>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Kiểm tra vi phạm SLA thành công.",
            Data = new
            {
                FeedbackSlaId = feedbackSlaId
            }
        });
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("UserId");

        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            throw new UnauthorizedAccessException(
                "Không tìm thấy User ID trong access token.");
        }

        if (!Guid.TryParse(
                userIdValue,
                out var currentUserId))
        {
            throw new UnauthorizedAccessException(
                "User ID trong access token không hợp lệ.");
        }

        return currentUserId;
    }
}