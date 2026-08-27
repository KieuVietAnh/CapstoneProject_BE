using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/management/staff-area-assignments")]
[Route("api/management/staff-responsibilities")]
public class ManagementStaffAreaAssignmentsController : ControllerBase
{
    private readonly IStaffAreaAssignmentService _staffAreaAssignmentService;

    public ManagementStaffAreaAssignmentsController(
        IStaffAreaAssignmentService staffAreaAssignmentService)
    {
        _staffAreaAssignmentService = staffAreaAssignmentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<StaffAreaAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(
        [FromQuery] Guid? userId = null,
        [FromQuery] int? areaId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] bool? isActive = null)
    {
        return Ok(await _staffAreaAssignmentService.GetAssignmentsAsync(
            GetCurrentUserId(),
            userId,
            areaId,
            categoryId,
            isActive,
            HttpContext.RequestAborted));
    }

    [HttpPost]
    [ProducesResponseType(typeof(StaffAreaAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAssignment(
        [FromBody] StaffAreaAssignmentCreateRequest request)
    {
        return Ok(await _staffAreaAssignmentService.CreateAsync(
            GetCurrentUserId(),
            request,
            HttpContext.RequestAborted));
    }

    [HttpPut("{assignmentId:int}")]
    [ProducesResponseType(typeof(StaffAreaAssignmentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAssignment(
        int assignmentId,
        [FromBody] StaffAreaAssignmentUpdateRequest request)
    {
        return Ok(await _staffAreaAssignmentService.UpdateAsync(
            GetCurrentUserId(),
            assignmentId,
            request,
            HttpContext.RequestAborted));
    }

    [HttpPatch("{assignmentId:int}/active")]
    [ProducesResponseType(typeof(StaffAreaAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(
        int assignmentId,
        [FromBody] SetActiveRequest request)
    {
        return Ok(await _staffAreaAssignmentService.SetActiveAsync(
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
