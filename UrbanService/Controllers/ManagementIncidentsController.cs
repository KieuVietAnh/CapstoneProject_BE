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
[Route("api/management/incidents")]
public sealed class ManagementIncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;
    private readonly IFeedbackService _feedbackService;

    public ManagementIncidentsController(
        IIncidentService incidentService,
        IFeedbackService feedbackService)
    {
        _incidentService = incidentService;
        _feedbackService = feedbackService;
    }

    /// <summary>Lấy queue Incident cho management.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<IncidentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidents([FromQuery] IncidentQueryParameters query)
    {
        return Ok(await _incidentService.GetIncidentsAsync(
            query,
            GetCurrentUserId(),
            HttpContext.RequestAborted));
    }

    /// <summary>Lấy chi tiết Incident, các Report, subscriber và event timeline.</summary>
    [HttpGet("{incidentId:guid}")]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIncident(Guid incidentId)
    {
        return Ok(await _incidentService.GetIncidentDetailAsync(
            incidentId,
            GetCurrentUserId(),
            HttpContext.RequestAborted));
    }

    /// <summary>Liên kết một Feedback/Report chưa có active link vào Incident.</summary>
    [HttpPost("{incidentId:guid}/reports")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkReport(Guid incidentId, [FromBody] LinkIncidentReportRequest request)
    {
        return Ok(await _incidentService.LinkReportAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));
    }

    /// <summary>Soft-unlink một Feedback/Report khỏi Incident và giữ audit history.</summary>
    [HttpDelete("{incidentId:guid}/reports/{feedbackId:guid}")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnlinkReport(Guid incidentId, Guid feedbackId)
    {
        await _incidentService.UnlinkReportAsync(
            incidentId,
            feedbackId,
            GetCurrentUserId(),
            HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Cập nhật dữ liệu điều phối của Incident, gồm Severity.</summary>
    [HttpPatch("{incidentId:guid}")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateIncident(Guid incidentId, [FromBody] UpdateIncidentRequest request)
        => Ok(await _incidentService.UpdateIncidentAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Chuyển trạng thái xử lý ở cấp Incident.</summary>
    [HttpPatch("{incidentId:guid}/status")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(Guid incidentId, [FromBody] UpdateIncidentStatusRequest request)
        => Ok(await _incidentService.UpdateStatusAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Lấy Staff phù hợp khu vực và danh mục của Incident.</summary>
    [HttpGet("{incidentId:guid}/assignee-candidates")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IReadOnlyCollection<IncidentAssigneeCandidateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssigneeCandidates(Guid incidentId)
        => Ok(await _incidentService.GetAssigneeCandidatesAsync(
            incidentId,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Phân công Staff xử lý Incident.</summary>
    [HttpPost("{incidentId:guid}/assign")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(Guid incidentId, [FromBody] AssignIncidentRequest request)
        => Ok(await _incidentService.AssignAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Lấy đơn vị xử lý phù hợp với khu vực và danh mục của Incident.</summary>
    [HttpGet("{incidentId:guid}/provider-candidates")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProviderCandidateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviderCandidates(Guid incidentId)
        => Ok(await _feedbackService.GetIncidentProviderCandidatesAsync(
            incidentId,
            GetCurrentUserId()));

    /// <summary>Lấy phân công đơn vị xử lý hiện tại của Incident.</summary>
    [HttpGet("{incidentId:guid}/provider-assignment")]
    [ProducesResponseType(typeof(IncidentProviderAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetProviderAssignment(Guid incidentId)
    {
        var assignment = await _feedbackService.GetCurrentProviderAssignmentAsync(
            incidentId,
            GetCurrentUserId());
        return assignment == null ? NoContent() : Ok(assignment);
    }

    /// <summary>Staff phân công một đơn vị xử lý cho Incident.</summary>
    /// <remarks>Mỗi Incident chỉ có một phân công và không hỗ trợ đổi đơn vị.</remarks>
    [HttpPost("{incidentId:guid}/provider-assignment")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(IncidentProviderAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignProvider(
        Guid incidentId,
        [FromBody] AssignIncidentProviderRequest request)
    {
        var assignment = await _feedbackService.AssignIncidentProviderAsync(
            incidentId,
            GetCurrentUserId(),
            request);
        return CreatedAtAction(
            nameof(GetProviderAssignment),
            new { incidentId },
            assignment);
    }

    /// <summary>Lấy kết quả xử lý đã gửi cho Incident.</summary>
    [HttpGet("{incidentId:guid}/resolutions")]
    [ProducesResponseType(typeof(IReadOnlyCollection<FeedbackResolutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResolutions(Guid incidentId)
        => Ok(await _feedbackService.GetIncidentResolutionsAsync(
            incidentId,
            GetCurrentUserId()));

    /// <summary>Staff gửi kết quả xử lý của Incident để Manager duyệt.</summary>
    [HttpPost("{incidentId:guid}/resolutions")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitResolution(
        Guid incidentId,
        [FromBody] SubmitResolutionRequest request)
    {
        await _feedbackService.SubmitIncidentResolutionAsync(
            incidentId,
            GetCurrentUserId(),
            request);
        return Ok(new { Message = "Resolution submitted successfully." });
    }

    /// <summary>Merge Incident nguồn vào Incident đích.</summary>
    [HttpPost("{incidentId:guid}/merge")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Merge(Guid incidentId, [FromBody] MergeIncidentRequest request)
        => Ok(await _incidentService.MergeAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Lấy timeline Incident có phân trang.</summary>
    [HttpGet("{incidentId:guid}/timeline")]
    [ProducesResponseType(typeof(PagedResultDto<IncidentEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeline(
        Guid incidentId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _incidentService.GetManagementTimelineAsync(
            incidentId,
            pageNumber,
            pageSize,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

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
