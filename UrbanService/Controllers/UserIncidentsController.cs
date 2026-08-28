using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SERVICEUSER)]
[Route("api/user/incidents")]
public sealed class UserIncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public UserIncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    /// <summary>Lấy danh sách sự vụ mà người dùng hiện tại đang theo dõi hoặc có phản ánh liên quan.</summary>
    /// <remarks>Chỉ `SERVICEUSER`; dữ liệu được giới hạn theo định danh lấy từ JWT.</remarks>
    [HttpGet("me")]
    [ProducesResponseType(typeof(PagedResultDto<IncidentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyIncidents([FromQuery] IncidentQueryParameters query)
        => Ok(await _incidentService.GetMyIncidentsAsync(GetCurrentUserId(), query, HttpContext.RequestAborted));

    /// <summary>Đăng ký theo dõi cập nhật của một sự vụ.</summary>
    /// <remarks>Chỉ `SERVICEUSER`; thao tác lặp lại không tạo đăng ký trùng.</remarks>
    [HttpPost("{incidentId:guid}/subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Subscribe(Guid incidentId)
    {
        await _incidentService.SubscribeAsync(incidentId, GetCurrentUserId(), HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Hủy đăng ký theo dõi một sự vụ.</summary>
    /// <remarks>Chỉ `SERVICEUSER`; thành công trả về `204 No Content`.</remarks>
    [HttpDelete("{incidentId:guid}/subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unsubscribe(Guid incidentId)
    {
        await _incidentService.UnsubscribeAsync(incidentId, GetCurrentUserId(), HttpContext.RequestAborted);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId)) throw new UnauthorizedAccessException();
        return userId;
    }
}
