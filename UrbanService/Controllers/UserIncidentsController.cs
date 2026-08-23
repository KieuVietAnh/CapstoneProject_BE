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

    [HttpGet("me")]
    [ProducesResponseType(typeof(PagedResultDto<IncidentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyIncidents([FromQuery] IncidentQueryParameters query)
        => Ok(await _incidentService.GetMyIncidentsAsync(GetCurrentUserId(), query, HttpContext.RequestAborted));

    [HttpPost("{incidentId:guid}/subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Subscribe(Guid incidentId)
    {
        await _incidentService.SubscribeAsync(incidentId, GetCurrentUserId(), HttpContext.RequestAborted);
        return NoContent();
    }

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
