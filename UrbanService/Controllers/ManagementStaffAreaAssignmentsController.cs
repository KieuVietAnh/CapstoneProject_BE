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

    /// <summary>Lấy danh sách phân công khu vực và danh mục của Staff.</summary>
    /// <remarks>
    /// `SYSTEMADMIN` xem toàn bộ. `INTERACTIONMANAGER` chỉ thấy phân công thuộc các khu vực đang quản lý.
    /// Có thể lọc theo Staff, khu vực, danh mục và trạng thái hoạt động.
    /// </remarks>
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

    /// <summary>Tạo phân công khu vực và danh mục cho Staff.</summary>
    /// <remarks>
    /// `SYSTEMADMIN` thao tác toàn hệ thống. `INTERACTIONMANAGER` chỉ tạo trong khu vực đang quản lý.
    /// Nếu phân công đang hoạt động đã tồn tại, dữ liệu được cập nhật theo request.
    /// </remarks>
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

    /// <summary>Cập nhật phạm vi phân công của Staff.</summary>
    /// <remarks>
    /// Với `INTERACTIONMANAGER`, cả khu vực hiện tại và khu vực mới đều phải thuộc phạm vi đang quản lý.
    /// </remarks>
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

    /// <summary>Thay đổi trạng thái hoạt động của phân công Staff.</summary>
    /// <remarks>
    /// `SYSTEMADMIN` có thể kích hoạt hoặc vô hiệu hóa. `INTERACTIONMANAGER` chỉ được vô hiệu hóa trong khu vực đang quản lý.
    /// </remarks>
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
