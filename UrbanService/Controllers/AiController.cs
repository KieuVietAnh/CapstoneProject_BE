using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.AI;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiClient _aiClient;
    private readonly IAiChatService _aiChatService;
    private readonly IUnitOfWork _uow;
    private readonly IAiFeedbackReviewQueue _aiFeedbackReviewQueue;
    private readonly IConfiguration _configuration;

    public AiController(
        IAiClient aiClient,
        IAiChatService aiChatService,
        IUnitOfWork uow,
        IAiFeedbackReviewQueue aiFeedbackReviewQueue,
        IConfiguration configuration)
    {
        _aiClient = aiClient;
        _aiChatService = aiChatService;
        _uow = uow;
        _aiFeedbackReviewQueue = aiFeedbackReviewQueue;
        _configuration = configuration;
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

    /// <summary>Kiem tra hang doi AI review feedback.</summary>
    [HttpGet("feedback-review-queue/status")]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(AiFeedbackReviewQueueStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> FeedbackReviewQueueStatus(CancellationToken cancellationToken)
    {
        var feedbacks = _uow.GetRepository<Feedback>().Entities.AsNoTracking();
        var isAiAvailable = await _aiClient.IsAvailableAsync(cancellationToken);

        var result = new AiFeedbackReviewQueueStatusResponse
        {
            PendingSubmittedCount = await feedbacks
                .CountAsync(f => f.Status == FeedbackStatus.Submitted, cancellationToken),
            QueuedInMemoryCount = _aiFeedbackReviewQueue.QueuedCount,
            RetryCooldownMinutes = int.TryParse(
                _configuration["AI:ReviewFailureRetryDelayMinutes"],
                out var retryDelayMinutes)
                ? retryDelayMinutes
                : 15,
            AiReviewedCount = await feedbacks
                .CountAsync(f => f.Status == FeedbackStatus.AiReviewed, cancellationToken),
            AnalysisResultCount = await _uow.GetRepository<AnalysisResult>().Entities
                .AsNoTracking()
                .CountAsync(cancellationToken),
            OldestSubmittedAt = await feedbacks
                .Where(f => f.Status == FeedbackStatus.Submitted)
                .OrderBy(f => f.CreatedAt)
                .Select(f => (DateTime?)f.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken),
            IsAiAvailable = isAiAvailable,
            Model = _aiClient.ModelName
        };

        return Ok(result);
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
