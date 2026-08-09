using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Route("api/integrations/messenger")]
public class MessengerController : ControllerBase
{
    private const string ManagementRoles =
        UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER;

    private readonly IMessengerService _messengerService;
    private readonly IMessengerWebhookQueue _webhookQueue;

    public MessengerController(
        IMessengerService messengerService,
        IMessengerWebhookQueue webhookQueue)
    {
        _messengerService = messengerService;
        _webhookQueue = webhookQueue;
    }

    /// <summary>Endpoint Meta dùng để xác minh callback URL.</summary>
    [HttpGet("webhook")]
    [AllowAnonymous]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (!_messengerService.IsVerificationRequestValid(mode, verifyToken))
        {
            return Forbid();
        }

        return Content(challenge ?? string.Empty, "text/plain", Encoding.UTF8);
    }

    /// <summary>Nhận sự kiện nhắn tin từ Messenger và đưa vào hàng đợi xử lý.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

        if (!_messengerService.IsSignatureValid(payload, signature))
        {
            return Unauthorized();
        }

        await _webhookQueue.EnqueueAsync(payload, cancellationToken);
        return Ok();
    }

    /// <summary>Xem draft và feedback đã tạo từ một người gửi Messenger.</summary>
    [HttpGet("conversations/{senderPsid}")]
    [Authorize(Roles = ManagementRoles)]
    [ProducesResponseType(typeof(MessengerConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversation(
        string senderPsid,
        CancellationToken cancellationToken)
    {
        var conversation = await _messengerService.GetConversationAsync(
            senderPsid,
            cancellationToken);
        return conversation == null ? NotFound() : Ok(conversation);
    }

    /// <summary>Xóa draft hiện tại và bắt đầu lại hội thoại với một người gửi.</summary>
    [HttpPost("conversations/{senderPsid}/reset")]
    [Authorize(Roles = ManagementRoles)]
    [ProducesResponseType(typeof(MessengerConversationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetConversation(
        string senderPsid,
        CancellationToken cancellationToken)
    {
        var conversation = await _messengerService.ResetConversationAsync(
            senderPsid,
            cancellationToken);
        return Ok(conversation);
    }
}
