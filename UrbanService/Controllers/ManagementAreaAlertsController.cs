using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMSTAFF)]
[Route("api/management/area-alerts")]
public class ManagementAreaAlertsController : ControllerBase
{
    private readonly IAreaAlertService _areaAlertService;

    public ManagementAreaAlertsController(IAreaAlertService areaAlertService)
    {
        _areaAlertService = areaAlertService;
    }

    /// <summary>Staff tạo cảnh báo khu vực thủ công.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserAreaAlertDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateManualAlert([FromBody] CreateAreaAlertRequest request)
    {
        var result = await _areaAlertService.CreateManualAlertAsync(GetCurrentUserId(), request);
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