using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMADMIN)]
[Route("api/admin/manager-area-assignments")]
public class AdminManagerAreaAssignmentsController : ControllerBase
{
    private readonly IManagerAreaAssignmentService _managerAreaAssignmentService;

    public AdminManagerAreaAssignmentsController(
        IManagerAreaAssignmentService managerAreaAssignmentService)
    {
        _managerAreaAssignmentService = managerAreaAssignmentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManagerAreaAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(
        [FromQuery] ManagerAreaAssignmentQueryParameters query)
    {
        return Ok(await _managerAreaAssignmentService.GetAssignmentsAsync(
            query,
            HttpContext.RequestAborted));
    }

    [HttpGet("{assignmentId:int}")]
    [ProducesResponseType(typeof(ManagerAreaAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAssignment(int assignmentId)
    {
        return Ok(await _managerAreaAssignmentService.GetAssignmentAsync(
            assignmentId,
            HttpContext.RequestAborted));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ManagerAreaAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAssignment(
        [FromBody] ManagerAreaAssignmentCreateRequest request)
    {
        return Ok(await _managerAreaAssignmentService.CreateAsync(
            GetCurrentUserId(),
            request,
            HttpContext.RequestAborted));
    }

    [HttpPut("{assignmentId:int}")]
    [ProducesResponseType(typeof(ManagerAreaAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAssignment(
        int assignmentId,
        [FromBody] ManagerAreaAssignmentUpdateRequest request)
    {
        return Ok(await _managerAreaAssignmentService.UpdateAsync(
            GetCurrentUserId(),
            assignmentId,
            request,
            HttpContext.RequestAborted));
    }

    [HttpPatch("{assignmentId:int}/active")]
    [ProducesResponseType(typeof(ManagerAreaAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(
        int assignmentId,
        [FromBody] ManagerAreaAssignmentActiveRequest request)
    {
        return Ok(await _managerAreaAssignmentService.SetActiveAsync(
            GetCurrentUserId(),
            assignmentId,
            request.IsActive,
            HttpContext.RequestAborted));
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
