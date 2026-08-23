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
[Route("api/management/incidents")]
public sealed class ManagementIncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public ManagementIncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    /// <summary>Lấy queue Incident cho management.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<IncidentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidents([FromQuery] IncidentQueryParameters query)
    {
        return Ok(await _incidentService.GetIncidentsAsync(query, HttpContext.RequestAborted));
    }

    /// <summary>Lấy chi tiết Incident, các Report, subscriber và event timeline.</summary>
    [HttpGet("{incidentId:guid}")]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIncident(Guid incidentId)
    {
        return Ok(await _incidentService.GetIncidentDetailAsync(incidentId, HttpContext.RequestAborted));
    }

    /// <summary>Liên kết một Feedback/Report chưa có active link vào Incident.</summary>
    [HttpPost("{incidentId:guid}/reports")]
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
