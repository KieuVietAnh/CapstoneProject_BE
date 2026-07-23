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
[Route("api/user/area-alerts")]
public class UserAreaAlertsController : ControllerBase
{
    private readonly IAreaAlertService _areaAlertService;

    public UserAreaAlertsController(IAreaAlertService areaAlertService)
    {
        _areaAlertService = areaAlertService;
    }

    /// <summary>User xem danh sách cảnh báo khu vực.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<UserAreaAlertDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAreaAlerts([FromQuery] AreaAlertQueryParameters query)
    {
        var result = await _areaAlertService.GetAlertsAsync(GetCurrentUserId(), query);
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