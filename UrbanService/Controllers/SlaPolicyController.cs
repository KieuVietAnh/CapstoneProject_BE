using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UrbanService.BLL.Common;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs.SLA;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Route("api/sla-policies")]
[Authorize]
public class SlaPolicyController : ControllerBase
{
    private readonly ISlaPolicyService _slaPolicyService;

    public SlaPolicyController(
        ISlaPolicyService slaPolicyService)
    {
        _slaPolicyService = slaPolicyService;
    }

    /// <summary>
    /// Lấy danh sách SLA policy có phân trang.
    /// </summary>
    /// <param name="query">
    /// Điều kiện tìm kiếm, lọc và phân trang.
    /// </param>
    /// <returns>
    /// Danh sách SLA policy và thông tin phân trang.
    /// </returns>
    /// <remarks>
    /// FE có thể truyền các query parameter:
    ///
    /// - search: tìm kiếm theo tên policy.
    /// - areaId: lọc theo khu vực.
    /// - categoryId: lọc theo category.
    /// - priority: Low, Medium, High hoặc Critical.
    /// - isActive: lọc policy đang bật hoặc đã tắt.
    /// - isCurrentlyEffective: lọc policy đang có hiệu lực.
    /// - pageNumber: trang hiện tại, mặc định 1.
    /// - pageSize: số bản ghi mỗi trang, mặc định 10, tối đa 100.
    ///
    /// Ví dụ:
    ///
    ///     GET /api/sla-policies?pageNumber=1&amp;pageSize=10
    ///
    ///     GET /api/sla-policies?priority=High&amp;isActive=true
    ///
    ///     GET /api/sla-policies?areaId=1&amp;categoryId=2
    ///
    ///     GET /api/sla-policies?search=giao%20thong
    ///
    ///     GET /api/sla-policies?isCurrentlyEffective=true
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResultDto<SlaPolicyDto>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] SlaPolicyQueryParameters query)
    {
        var result =
            await _slaPolicyService.GetAllAsync(query);

        return Ok(
            new ApiResponse<PagedResultDto<SlaPolicyDto>>
            {
                Status = StatusCodes.Status200OK,
                Msg = "Lấy danh sách SLA policy thành công.",
                Data = result
            });
    }

    /// <summary>
    /// Lấy chi tiết một SLA policy.
    /// </summary>
    /// <param name="slaPolicyId">
    /// ID của SLA policy cần lấy.
    /// </param>
    /// <returns>
    /// Thông tin chi tiết SLA policy.
    /// </returns>
    /// <remarks>
    /// Ví dụ:
    ///
    ///     GET /api/sla-policies/1
    /// </remarks>
    [HttpGet("{slaPolicyId:int}")]
    [ProducesResponseType(
        typeof(ApiResponse<SlaPolicyDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(
        int slaPolicyId)
    {
        var result =
            await _slaPolicyService.GetByIdAsync(slaPolicyId);

        return Ok(new ApiResponse<SlaPolicyDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Lấy thông tin SLA policy thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Tạo SLA policy mới.
    /// </summary>
    /// <param name="request">
    /// Thông tin policy cần tạo.
    /// </param>
    /// <returns>
    /// SLA policy vừa được tạo.
    /// </returns>
    /// <remarks>
    /// Chỉ Admin và Manager được phép tạo policy.
    ///
    /// Quy tắc:
    ///
    /// - AreaId bằng null: áp dụng cho tất cả khu vực.
    /// - CategoryId bằng null: áp dụng cho tất cả category.
    /// - Priority: Low, Medium, High hoặc Critical.
    /// - ResponseTimeMinutes phải lớn hơn 0.
    /// - ResolutionTimeMinutes phải lớn hơn hoặc bằng ResponseTimeMinutes.
    /// - EffectiveTo có thể null nếu policy không xác định ngày hết hạn.
    ///
    /// Ví dụ body:
    ///
    ///     {
    ///       "policyName": "SLA sự cố giao thông mức cao",
    ///       "areaId": 1,
    ///       "categoryId": 2,
    ///       "priority": "High",
    ///       "responseTimeMinutes": 30,
    ///       "resolutionTimeMinutes": 240,
    ///       "effectiveFrom": "2026-07-30T00:00:00Z",
    ///       "effectiveTo": null,
    ///       "isActive": true
    ///     }
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(
        typeof(ApiResponse<SlaPolicyDto>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] SlaPolicyCreateRequest request)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _slaPolicyService.CreateAsync(
            currentUserId,
            request);

        var response = new ApiResponse<SlaPolicyDto>
        {
            Status = StatusCodes.Status201Created,
            Msg = "Tạo SLA policy thành công.",
            Data = result
        };

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                slaPolicyId = result.SlaPolicyId
            },
            response);
    }

    /// <summary>
    /// Cập nhật toàn bộ SLA policy.
    /// </summary>
    /// <param name="slaPolicyId">
    /// ID của SLA policy cần cập nhật.
    /// </param>
    /// <param name="request">
    /// Toàn bộ dữ liệu mới của policy.
    /// </param>
    /// <returns>
    /// SLA policy sau khi cập nhật.
    /// </returns>
    /// <remarks>
    /// Đây là API PUT nên FE cần gửi đầy đủ các trường.
    ///
    /// Ví dụ:
    ///
    ///     PUT /api/sla-policies/1
    ///
    /// Body:
    ///
    ///     {
    ///       "policyName": "SLA giao thông mức cao",
    ///       "areaId": 1,
    ///       "categoryId": 2,
    ///       "priority": "High",
    ///       "responseTimeMinutes": 20,
    ///       "resolutionTimeMinutes": 180,
    ///       "effectiveFrom": "2026-07-30T00:00:00Z",
    ///       "effectiveTo": null,
    ///       "isActive": true
    ///     }
    /// </remarks>
    [HttpPut("{slaPolicyId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(
        typeof(ApiResponse<SlaPolicyDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int slaPolicyId,
        [FromBody] SlaPolicyUpdateRequest request)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _slaPolicyService.UpdateAsync(
            currentUserId,
            slaPolicyId,
            request);

        return Ok(new ApiResponse<SlaPolicyDto>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Cập nhật SLA policy thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Kích hoạt hoặc ngừng kích hoạt SLA policy.
    /// </summary>
    /// <param name="slaPolicyId">
    /// ID của SLA policy.
    /// </param>
    /// <param name="request">
    /// Trạng thái mới của policy.
    /// </param>
    /// <returns>
    /// ID và trạng thái mới của SLA policy.
    /// </returns>
    /// <remarks>
    /// Chỉ Admin và Manager được phép thay đổi trạng thái.
    ///
    /// Kích hoạt:
    ///
    ///     PATCH /api/sla-policies/1/active
    ///
    ///     {
    ///       "isActive": true
    ///     }
    ///
    /// Ngừng kích hoạt:
    ///
    ///     PATCH /api/sla-policies/1/active
    ///
    ///     {
    ///       "isActive": false
    ///     }
    /// </remarks>
    [HttpPatch("{slaPolicyId:int}/active")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetActive(
        int slaPolicyId,
        [FromBody] SlaPolicySetActiveRequest request)
    {
        var currentUserId = GetCurrentUserId();

        await _slaPolicyService.SetActiveAsync(
            currentUserId,
            slaPolicyId,
            request.IsActive);

        return Ok(new ApiResponse<object>
        {
            Status = StatusCodes.Status200OK,
            Msg = request.IsActive
                ? "Kích hoạt SLA policy thành công."
                : "Ngừng kích hoạt SLA policy thành công.",
            Data = new
            {
                SlaPolicyId = slaPolicyId,
                request.IsActive
            }
        });
    }

    /// <summary>
    /// Xóa SLA policy chưa từng được sử dụng.
    /// </summary>
    /// <param name="slaPolicyId">
    /// ID của SLA policy cần xóa.
    /// </param>
    /// <returns>
    /// Kết quả xóa SLA policy.
    /// </returns>
    /// <remarks>
    /// Chỉ Admin được phép xóa policy.
    ///
    /// Policy đã được gắn vào FeedbackSla sẽ không thể xóa.
    /// FE cần sử dụng endpoint thay đổi trạng thái để tắt policy đó.
    ///
    /// Ví dụ:
    ///
    ///     DELETE /api/sla-policies/1
    /// </remarks>
    [HttpDelete("{slaPolicyId:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        int slaPolicyId)
    {
        await _slaPolicyService.DeleteAsync(slaPolicyId);

        return Ok(new ApiResponse<object>
        {
            Status = StatusCodes.Status200OK,
            Msg = "Xóa SLA policy thành công.",
            Data = new
            {
                SlaPolicyId = slaPolicyId
            }
        });
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("UserId");

        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            throw new UnauthorizedAccessException(
                "Không tìm thấy User ID trong access token.");
        }

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException(
                "User ID trong access token không hợp lệ.");
        }

        return userId;
    }
}