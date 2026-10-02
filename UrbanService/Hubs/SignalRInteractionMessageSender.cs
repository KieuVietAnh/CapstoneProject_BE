using Microsoft.AspNetCore.SignalR;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Hubs;

public class SignalRInteractionMessageSender : IRealtimeInteractionMessageSender
{
    private readonly IHubContext<InteractionMessageHub> _hubContext;

    private readonly ILogger<SignalRInteractionMessageSender> _logger;

    public SignalRInteractionMessageSender(
        IHubContext<InteractionMessageHub> hubContext,
        ILogger<SignalRInteractionMessageSender> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendTicketMessageAsync(
        Guid feedbackId,
        InteractionMessageDto message,
        CancellationToken cancellationToken = default)
    {
        /*
         * Ghi chú nội bộ chỉ đi vào group nhân sự. Người dân đang theo dõi ticket
         * nằm ở group chung nên không nhận được gói tin này, tức là nội dung nội bộ
         * không rời khỏi server xuống máy họ.
         */
        var group = message.IsInternal
            ? InteractionMessageHub.TicketInternalGroup(feedbackId)
            : InteractionMessageHub.TicketGroup(feedbackId);

        await _hubContext.Clients.Group(group)
            .SendAsync("TicketMessageReceived", message, cancellationToken);

        _logger.LogInformation(
            "SignalR event TicketMessageReceived sent to group {Group}. MessageId: {MessageId}",
            group,
            message.InteractionMessageId);
    }
}
