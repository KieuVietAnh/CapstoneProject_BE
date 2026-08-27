using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize]
[Route("api/feedbacks/{feedbackId:guid}/messages")]
public class InteractionMessagesController : ControllerBase
{
    private readonly IInteractionMessageService _interactionMessageService;

    public InteractionMessagesController(IInteractionMessageService interactionMessageService)
    {
        _interactionMessageService = interactionMessageService;
    }

    /// <summary>Lấy danh sách trao đổi/chat theo ticket.</summary>
    /// <remarks>
    /// Resident chỉ xem được tin nhắn public của ticket do chính mình tạo.
    /// Staff/Manager/SystemAdmin xem được tin nhắn public; truyền `includeInternal=true` để xem cả ghi chú nội bộ.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<InteractionMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketMessages(
        [FromRoute] Guid feedbackId,
        [FromQuery] bool includeInternal = false)
    {
        var result = await _interactionMessageService.GetTicketMessagesAsync(
            GetCurrentUserId(),
            feedbackId,
            includeInternal);

        return Ok(result);
    }

    /// <summary>Gửi tin nhắn trao đổi theo ticket.</summary>
    /// <remarks>
    /// Resident được gửi tin nhắn public cho ticket của mình.
    /// Staff/Manager/SystemAdmin được gửi tin nhắn public hoặc ghi chú nội bộ (`isInternal=true`).
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(InteractionMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendMessage(
        [FromRoute] Guid feedbackId,
        [FromBody] InteractionMessageCreateRequest request)
    {
        var result = await _interactionMessageService.SendMessageAsync(
            GetCurrentUserId(),
            feedbackId,
            request);

        return Ok(result);
    }

    /// <summary>Tạo system message cho ticket.</summary>
    /// <remarks>
    /// Chỉ SystemAdmin/InteractionManager được tạo system message. Mặc định là ghi chú nội bộ.
    /// </remarks>
    [HttpPost("system")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(InteractionMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddSystemMessage(
        [FromRoute] Guid feedbackId,
        [FromBody] SystemInteractionMessageCreateRequest request)
    {
        var result = await _interactionMessageService.AddSystemMessageAsync(
            GetCurrentUserId(),
            feedbackId,
            request);

        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new UnauthorizedAccessException("Invalid user id in token.");
        }

        return parsedUserId;
    }
}
