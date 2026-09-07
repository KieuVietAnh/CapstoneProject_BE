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
    /// Manager/Admin bắt đầu SLA cho sự vụ sau khi xác minh.
    /// </summary>
    [HttpPost("incident/{incidentId:guid}/start")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentSlaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Start(
        Guid incidentId)
    {
        var result =
            await _slaService.StartAsync(
                incidentId,
                GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Lấy SLA hiện tại của sự vụ.
    /// </summary>
    [HttpGet("incident/{incidentId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(IncidentSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(
        Guid incidentId)
    {
        var result =
            await _slaService.GetCurrentByIncidentIdAsync(
                incidentId,
                GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Nhân viên ghi nhận phản hồi đầu tiên.
    /// </summary>
    [HttpPatch("incident/{incidentId:guid}/responded")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(IncidentSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkResponded(
        Guid incidentId,
        [FromBody] string? note)
    {
        var result =
            await _slaService.MarkRespondedAsync(
                incidentId,
                GetCurrentUserId(),
                note);

        return Ok(result);
    }



    /// <summary>
    /// Manager/Admin tạm dừng SLA.
    /// </summary>
    [HttpPost("incident/{incidentId:guid}/pause")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pause(
        Guid incidentId,
        [FromBody] PauseSlaRequest request)
    {
        var result =
            await _slaService.PauseAsync(
                incidentId,
                GetCurrentUserId(),
                request);

        return Ok(result);
    }



    /// <summary>
    /// Manager/Admin tiếp tục SLA.
    /// </summary>
    [HttpPost("incident/{incidentId:guid}/resume")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resume(
        Guid incidentId,
        [FromBody] ResumeSlaRequest request)
    {
        var result =
            await _slaService.ResumeAsync(
                incidentId,
                GetCurrentUserId(),
                request);

        return Ok(result);
    }



    /// <summary>
    /// Hoàn thành SLA sau khi xử lý xong.
    /// </summary>
    [HttpPost("incident/{incidentId:guid}/complete")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(
        Guid incidentId,
        [FromBody] CompleteSlaRequest request)
    {
        var result =
            await _slaService.CompleteAsync(
                incidentId,
                GetCurrentUserId(),
                request);

        return Ok(result);
    }



    /// <summary>
    /// Manager/Admin tính lại SLA khi thay đổi Category/Priority.
    /// </summary>
    [HttpPost("incident/{incidentId:guid}/recalculate")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recalculate(
        Guid incidentId,
        [FromBody] RecalculateSlaRequest request)
    {
        var result =
            await _slaService.RecalculateAsync(
                incidentId,
                GetCurrentUserId(),
                request);

        return Ok(result);
    }



    /// <summary>
    /// Hủy SLA.
    /// </summary>
    [HttpPost("incident/{incidentId:guid}/cancel")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentSlaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        Guid incidentId,
        [FromBody] string? note)
    {
        var result =
            await _slaService.CancelAsync(
                incidentId,
                GetCurrentUserId(),
                note);

        return Ok(result);
    }



    /// <summary>
    /// Kiểm tra vi phạm SLA thủ công.
    /// </summary>
    /// <remarks>Chỉ `INTERACTIONMANAGER`; cập nhật trạng thái vi phạm dựa trên thời điểm hiện tại.</remarks>
    [HttpPost("{incidentSlaId:long}/check")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckViolation(
        long incidentSlaId)
    {
        await _slaService.CheckViolationAsync(
            incidentSlaId,
            GetCurrentUserId());

        return Ok();
    }



    /// <summary>
    /// Lấy trạng thái SLA.
    /// </summary>
    [HttpGet("incident/{incidentId:guid}/status")]
    [Authorize]
    [ProducesResponseType(typeof(SlaStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(
        Guid incidentId)
    {
        var result =
            await _slaService.GetStatusAsync(
                incidentId,
                GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Lấy timeline SLA.
    /// </summary>
    [HttpGet("incident/{incidentId:guid}/timeline")]
    [Authorize]
    [ProducesResponseType(typeof(List<SlaTimelineDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeline(
        Guid incidentId)
    {
        var result =
            await _slaService.GetTimelineAsync(
                incidentId,
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
