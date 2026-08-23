using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Route("api/integrations/zalo")]
public class ZaloController : ControllerBase
{
    private const string ManagementRoles =
        UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER;

    private readonly IZaloService _zaloService;
    private readonly IZaloWebhookInbox _webhookInbox;
    private readonly IZaloWebhookQueue _webhookQueue;
    private readonly bool _isEnabled;

    public ZaloController(
        IZaloService zaloService,
        IZaloWebhookInbox webhookInbox,
        IZaloWebhookQueue webhookQueue,
        IConfiguration configuration)
    {
        _zaloService = zaloService;
        _webhookInbox = webhookInbox;
        _webhookQueue = webhookQueue;
        _isEnabled = configuration.GetValue("Zalo:Enabled", false);
    }

    /// <summary>Nhận sự kiện từ Zalo OA và đưa vào hàng đợi xử lý.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        if (!_isEnabled)
        {
            return NotFound();
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["X-ZEvent-Signature"].FirstOrDefault();

        if (!_zaloService.IsSignatureValid(payload, signature))
        {
            return Unauthorized();
        }

        var webhookEventId = await _webhookInbox.StoreAsync(payload, cancellationToken);
        if (webhookEventId.HasValue)
        {
            await _webhookQueue.EnqueueAsync(webhookEventId.Value, cancellationToken);
        }

        return Ok();
    }

    /// <summary>Xem draft và phản ánh gần nhất của một người gửi Zalo.</summary>
    [HttpGet("conversations/{senderUserId}")]
    [Authorize(Roles = ManagementRoles)]
    [ProducesResponseType(typeof(ZaloConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversation(
        string senderUserId,
        CancellationToken cancellationToken)
    {
        if (!_isEnabled)
        {
            return NotFound();
        }

        var conversation = await _zaloService.GetConversationAsync(
            senderUserId,
            cancellationToken);
        return conversation == null ? NotFound() : Ok(conversation);
    }

    /// <summary>Xóa draft hiện tại và bắt đầu lại hội thoại Zalo.</summary>
    [HttpPost("conversations/{senderUserId}/reset")]
    [Authorize(Roles = ManagementRoles)]
    [ProducesResponseType(typeof(ZaloConversationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetConversation(
        string senderUserId,
        CancellationToken cancellationToken)
    {
        if (!_isEnabled)
        {
            return NotFound();
        }

        var conversation = await _zaloService.ResetConversationAsync(
            senderUserId,
            cancellationToken);
        return Ok(conversation);
    }
}
