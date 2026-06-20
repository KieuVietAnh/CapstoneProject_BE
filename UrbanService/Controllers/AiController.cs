using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.AI;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiClient _aiClient;
    private readonly IAiChatService _aiChatService;

    public AiController(IAiClient aiClient, IAiChatService aiChatService)
    {
        _aiClient = aiClient;
        _aiChatService = aiChatService;
    }

    /// <summary>Kiem tra BE co ket noi duoc AI server khong.</summary>
    [HttpGet("health")]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(AiHealthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var isAvailable = await _aiClient.IsAvailableAsync(cancellationToken);
        return Ok(new AiHealthResponse
        {
            IsAvailable = isAvailable,
            Model = _aiClient.ModelName,
            Error = isAvailable ? null : "AI server is not available."
        });
    }

    /// <summary>Chatbot UrbanService cho nguoi dan.</summary>
    [HttpGet("conversations/me")]
    [Authorize(Roles = UserRole.SERVICEUSER)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AiConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyConversations(CancellationToken cancellationToken)
    {
        var result = await _aiChatService.GetMyConversationsAsync(
            GetCurrentUserId(),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Lay cac message cu trong mot AI conversation cua nguoi dung hien tai.</summary>
    [HttpGet("conversations/{conversationId:int}/messages")]
    [Authorize(Roles = UserRole.SERVICEUSER)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AiMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetConversationMessages(
        int conversationId,
        CancellationToken cancellationToken)
    {
        var result = await _aiChatService.GetConversationMessagesAsync(
            GetCurrentUserId(),
            conversationId,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Chatbot UrbanService cho nguoi dan.</summary>
    [HttpPost("chat")]
    [Authorize(Roles = UserRole.SERVICEUSER)]
    [ProducesResponseType(typeof(AiChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Chat(
        [FromBody] AiChatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _aiChatService.SendAsync(
            GetCurrentUserId(),
            request,
            cancellationToken);

        return Ok(result);
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
