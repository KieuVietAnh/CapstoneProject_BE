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

    /// <summary>Lấy danh sách phạm vi khu vực được phân cho Interaction Manager.</summary>
    /// <remarks>
    /// Chỉ `SYSTEMADMIN` được truy cập. Có thể lọc theo Manager, khu vực và trạng thái hoạt động.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManagerAreaAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(
        [FromQuery] ManagerAreaAssignmentQueryParameters query)
    {
        return Ok(await _managerAreaAssignmentService.GetAssignmentsAsync(
            query,
            HttpContext.RequestAborted));
    }

    /// <summary>Lấy chi tiết một phạm vi quản lý của Interaction Manager.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được truy cập.</remarks>
    [HttpGet("{assignmentId:int}")]
    [ProducesResponseType(typeof(ManagerAreaAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAssignment(int assignmentId)
    {
        return Ok(await _managerAreaAssignmentService.GetAssignmentAsync(
            assignmentId,
            HttpContext.RequestAborted));
    }

    /// <summary>Phân một khu vực cho Interaction Manager.</summary>
    /// <remarks>
    /// Chỉ `SYSTEMADMIN` được thao tác. Nếu phạm vi đã tồn tại, hệ thống kích hoạt lại bản ghi đó.
    /// </remarks>
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

    /// <summary>Cập nhật khu vực trong phạm vi quản lý của Interaction Manager.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được thao tác; cặp Manager và khu vực phải là duy nhất.</remarks>
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

    /// <summary>Kích hoạt hoặc vô hiệu hóa phạm vi quản lý của Interaction Manager.</summary>
    /// <remarks>Chỉ `SYSTEMADMIN` được thao tác.</remarks>
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
