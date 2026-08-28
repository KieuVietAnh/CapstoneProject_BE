using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.SLA;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Route("api/slas")]
public class SlaController : ControllerBase
{
    private readonly ISlaService _slaService;

    public SlaController(ISlaService slaService)
    {
        _slaService = slaService;
    }


    /// <summary>
    /// Manager/Admin bắt đầu SLA cho feedback sau khi xác minh.
    /// </summary>
    [HttpPost("feedback/{feedbackId:guid}/start")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackSlaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Start(
        Guid feedbackId)
    {
        var result =
            await _slaService.StartAsync(
                feedbackId,
                GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Lấy SLA hiện tại của feedback.
    /// </summary>
    [HttpGet("feedback/{feedbackId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(FeedbackSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(
        Guid feedbackId)
    {
        var result =
            await _slaService.GetCurrentByFeedbackIdAsync(
                feedbackId,
                GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Nhân viên ghi nhận phản hồi đầu tiên.
    /// </summary>
    [HttpPatch("feedback/{feedbackId:guid}/responded")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(FeedbackSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkResponded(
        Guid feedbackId,
        [FromBody] string? note)
    {
        var result =
            await _slaService.MarkRespondedAsync(
                feedbackId,
                GetCurrentUserId(),
                note);

        return Ok(result);
    }



    /// <summary>
    /// Manager/Admin tạm dừng SLA.
    /// </summary>
    [HttpPost("feedback/{feedbackId:guid}/pause")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pause(
        Guid feedbackId,
        [FromBody] PauseSlaRequest request)
    {
        var result =
            await _slaService.PauseAsync(
                feedbackId,
                GetCurrentUserId(),
                request);

        return Ok(result);
    }



    /// <summary>
    /// Manager/Admin tiếp tục SLA.
    /// </summary>
    [HttpPost("feedback/{feedbackId:guid}/resume")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resume(
        Guid feedbackId,
        [FromBody] ResumeSlaRequest request)
    {
        var result =
            await _slaService.ResumeAsync(
                feedbackId,
                GetCurrentUserId(),
                request);

        return Ok(result);
    }



    /// <summary>
    /// Hoàn thành SLA sau khi xử lý xong.
    /// </summary>
    [HttpPost("feedback/{feedbackId:guid}/complete")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(
        Guid feedbackId,
        [FromBody] CompleteSlaRequest request)
    {
        var result =
            await _slaService.CompleteAsync(
                feedbackId,
                GetCurrentUserId(),
                request);

        return Ok(result);
    }



    /// <summary>
    /// Manager/Admin tính lại SLA khi thay đổi Category/Priority.
    /// </summary>
    [HttpPost("feedback/{feedbackId:guid}/recalculate")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recalculate(
        Guid feedbackId,
        [FromBody] RecalculateSlaRequest request)
    {
        var result =
            await _slaService.RecalculateAsync(
                feedbackId,
                GetCurrentUserId(),
                request);

        return Ok(result);
    }



    /// <summary>
    /// Hủy SLA.
    /// </summary>
    [HttpPost("feedback/{feedbackId:guid}/cancel")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        Guid feedbackId,
        [FromBody] string? note)
    {
        var result =
            await _slaService.CancelAsync(
                feedbackId,
                GetCurrentUserId(),
                note);

        return Ok(result);
    }



    /// <summary>
    /// Kiểm tra vi phạm SLA thủ công.
    /// </summary>
    /// <remarks>Chỉ `INTERACTIONMANAGER`; cập nhật trạng thái vi phạm dựa trên thời điểm hiện tại.</remarks>
    [HttpPost("{feedbackSlaId:long}/check")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckViolation(
        long feedbackSlaId)
    {
        await _slaService.CheckViolationAsync(
            feedbackSlaId,
            GetCurrentUserId());

        return Ok();
    }



    /// <summary>
    /// Lấy trạng thái SLA.
    /// </summary>
    [HttpGet("feedback/{feedbackId:guid}/status")]
    [Authorize]
    [ProducesResponseType(typeof(SlaStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(
        Guid feedbackId)
    {
        var result =
            await _slaService.GetStatusAsync(
                feedbackId,
                GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Lấy timeline SLA.
    /// </summary>
    [HttpGet("feedback/{feedbackId:guid}/timeline")]
    [Authorize]
    [ProducesResponseType(typeof(List<SlaTimelineDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeline(
        Guid feedbackId)
    {
        var result =
            await _slaService.GetTimelineAsync(
                feedbackId,
                GetCurrentUserId());

        return Ok(result);
    }



    private Guid GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
            userId,
            out var parsedUserId))
        {
            throw new UnauthorizedAccessException();
        }

        return parsedUserId;
    }
}
