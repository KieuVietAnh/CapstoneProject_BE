using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.RateLimiting;

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
    [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Subscribe(Guid incidentId)
    {
        await _incidentService.SubscribeAsync(incidentId, GetCurrentUserId(), HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Hủy đăng ký theo dõi một sự vụ.</summary>
    /// <remarks>Chỉ `SERVICEUSER`; thành công trả về `204 No Content`.</remarks>
    [HttpDelete("{incidentId:guid}/subscribe")]
    [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unsubscribe(Guid incidentId)
    {
        await _incidentService.UnsubscribeAsync(incidentId, GetCurrentUserId(), HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Thêm bình luận trực tiếp vào một sự vụ công khai.</summary>
    [HttpPost("{incidentId:guid}/comments")]
    [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
    [ProducesResponseType(typeof(IncidentCommentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddComment(
        Guid incidentId,
        [FromBody] IncidentCommentCreateRequest request)
        => Ok(await _incidentService.AddCommentAsync(
            incidentId,
            GetCurrentUserId(),
            request,
            HttpContext.RequestAborted));

    /// <summary>Sửa bình luận do chính người dùng hiện tại tạo trên một sự vụ.</summary>
    [HttpPatch("{incidentId:guid}/comments/{incidentCommentId:guid}")]
    [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
    [ProducesResponseType(typeof(IncidentCommentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateComment(
        Guid incidentId,
        Guid incidentCommentId,
        [FromBody] IncidentCommentUpdateRequest request)
        => Ok(await _incidentService.UpdateCommentAsync(
            incidentId,
            incidentCommentId,
            GetCurrentUserId(),
            request,
            HttpContext.RequestAborted));

    /// <summary>Xóa bình luận do chính người dùng hiện tại tạo trên một sự vụ.</summary>
    [HttpDelete("{incidentId:guid}/comments/{incidentCommentId:guid}")]
    [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteComment(
        Guid incidentId,
        Guid incidentCommentId)
    {
        await _incidentService.DeleteCommentAsync(
            incidentId,
            incidentCommentId,
            GetCurrentUserId(),
            HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Đồng tình với một sự vụ công khai.</summary>
    /// <remarks>Gọi lặp lại không tạo upvote trùng.</remarks>
    [HttpPost("{incidentId:guid}/support")]
    [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Support(Guid incidentId)
    {
        await _incidentService.SupportAsync(
            incidentId,
            GetCurrentUserId(),
            HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Hủy đồng tình với một sự vụ công khai.</summary>
    /// <remarks>Nếu chưa đồng tình, API vẫn trả về thành công.</remarks>
    [HttpDelete("{incidentId:guid}/support")]
    [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unsupport(Guid incidentId)
    {
        await _incidentService.UnsupportAsync(
            incidentId,
            GetCurrentUserId(),
            HttpContext.RequestAborted);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId)) throw new UnauthorizedAccessException();
        return userId;
    }
}
