using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.RateLimiting;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SERVICEUSER)]
[Route("api/user/messenger-links")]
public class UserMessengerLinksController : ControllerBase
{
    private readonly IMessengerAccountLinkService _messengerAccountLinkService;

    public UserMessengerLinksController(
        IMessengerAccountLinkService messengerAccountLinkService)
    {
        _messengerAccountLinkService = messengerAccountLinkService;
    }

    /// <summary>Lấy các liên kết Messenger của tài khoản hiện tại.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<MessengerAccountLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyLinks(CancellationToken cancellationToken)
    {
        var result = await _messengerAccountLinkService.GetMyLinksAsync(
            GetCurrentUserId(),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Xác nhận liên kết tài khoản web với Messenger bằng token dùng một lần.</summary>
    [HttpPost("confirm")]
    [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
    [ProducesResponseType(typeof(MessengerAccountLinkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Confirm(
        [FromBody] ConfirmMessengerAccountLinkRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _messengerAccountLinkService.ConfirmAsync(
            GetCurrentUserId(),
            request.Token,
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Hủy một liên kết Messenger của tài khoản hiện tại.</summary>
    [HttpDelete("{linkId:long}")]
    [EnableRateLimiting(RateLimitPolicyNames.UserWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Revoke(
        long linkId,
        CancellationToken cancellationToken)
    {
        await _messengerAccountLinkService.RevokeAsync(
            GetCurrentUserId(),
            linkId,
            cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawUserId, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }
}
