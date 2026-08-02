using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
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
    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResultDto<SlaPolicyDto>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    /// Lấy chi tiết SLA policy.
    /// </summary>
    [HttpGet("{slaPolicyId:int}")]
    [ProducesResponseType(
        typeof(ApiResponse<SlaPolicyDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int slaPolicyId)
    {
        var result =
            await _slaPolicyService.GetByIdAsync(
                slaPolicyId);

        return Ok(
            new ApiResponse<SlaPolicyDto>
            {
                Status = StatusCodes.Status200OK,
                Msg = "Lấy thông tin SLA policy thành công.",
                Data = result
            });
    }



    /// <summary>
    /// Tạo SLA policy mới.
    /// </summary>
    [HttpPost]
    [Authorize(Roles =
        UserRole.SYSTEMADMIN + "," +
        UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(
        typeof(ApiResponse<SlaPolicyDto>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] SlaPolicyCreateRequest request)
    {
        var currentUserId =
            GetCurrentUserId();

        var result =
            await _slaPolicyService.CreateAsync(
                currentUserId,
                request);


        var response =
            new ApiResponse<SlaPolicyDto>
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
    /// Cập nhật SLA policy.
    /// </summary>
    [HttpPut("{slaPolicyId:int}")]
    [Authorize(Roles =
        UserRole.SYSTEMADMIN + "," +
        UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(
        typeof(ApiResponse<SlaPolicyDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int slaPolicyId,
        [FromBody] SlaPolicyUpdateRequest request)
    {
        var currentUserId =
            GetCurrentUserId();


        var result =
            await _slaPolicyService.UpdateAsync(
                currentUserId,
                slaPolicyId,
                request);


        return Ok(
            new ApiResponse<SlaPolicyDto>
            {
                Status = StatusCodes.Status200OK,
                Msg = "Cập nhật SLA policy thành công.",
                Data = result
            });
    }



    /// <summary>
    /// Kích hoạt hoặc vô hiệu hóa SLA policy.
    /// </summary>
    [HttpPatch("{slaPolicyId:int}/active")]
    [Authorize(Roles =
        UserRole.SYSTEMADMIN + "," +
        UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActive(
        int slaPolicyId,
        [FromBody] SlaPolicySetActiveRequest request)
    {
        var currentUserId =
            GetCurrentUserId();


        await _slaPolicyService.SetActiveAsync(
            currentUserId,
            slaPolicyId,
            request.IsActive);


        return Ok(
            new ApiResponse<object>
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
    /// Xóa SLA policy.
    /// </summary>
    [HttpDelete("{slaPolicyId:int}")]
    [Authorize(Roles =
        UserRole.SYSTEMADMIN)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        int slaPolicyId)
    {
        await _slaPolicyService.DeleteAsync(
            slaPolicyId);


        return Ok(
            new ApiResponse<object>
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
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);


        if (!Guid.TryParse(
            userId,
            out var parsedUserId))
        {
            throw new UnauthorizedAccessException();
        }


        return parsedUserId;
    }
}