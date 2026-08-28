using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/incidents")]
public sealed class PublicIncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public PublicIncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    /// <summary>Lấy danh sách sự vụ đô thị được công khai.</summary>
    /// <remarks>API công khai, không yêu cầu JWT; hỗ trợ bộ lọc và phân trang từ query.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<PublicIncidentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidents([FromQuery] IncidentQueryParameters query)
        => Ok(await _incidentService.GetPublicIncidentsAsync(query, HttpContext.RequestAborted));

    /// <summary>Lấy chi tiết một sự vụ công khai.</summary>
    /// <remarks>
    /// API không yêu cầu JWT. Nếu có JWT hợp lệ, response có thể phản ánh trạng thái đăng ký theo dõi của người dùng hiện tại.
    /// </remarks>
    [HttpGet("{incidentId:guid}")]
    [ProducesResponseType(typeof(PublicIncidentDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncident(Guid incidentId)
        => Ok(await _incidentService.GetPublicIncidentDetailAsync(
            incidentId,
            GetCurrentUserIdOrEmpty(),
            HttpContext.RequestAborted));

    /// <summary>Lấy các phản ánh công khai đang liên kết với một sự vụ.</summary>
    /// <remarks>API công khai và chỉ trả projection an toàn cho người dân.</remarks>
    [HttpGet("{incidentId:guid}/reports")]
    [ProducesResponseType(typeof(IReadOnlyCollection<PublicIncidentReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReports(Guid incidentId)
        => Ok(await _incidentService.GetPublicIncidentReportsAsync(incidentId, HttpContext.RequestAborted));

    /// <summary>Lấy dòng thời gian công khai của một sự vụ.</summary>
    /// <remarks>API công khai; hỗ trợ phân trang bằng `pageNumber` và `pageSize`.</remarks>
    [HttpGet("{incidentId:guid}/timeline")]
    [ProducesResponseType(typeof(PagedResultDto<PublicIncidentEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeline(
        Guid incidentId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _incidentService.GetPublicTimelineAsync(
            incidentId,
            pageNumber,
            pageSize,
            HttpContext.RequestAborted));

    private Guid GetCurrentUserIdOrEmpty()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : Guid.Empty;
    }
}
