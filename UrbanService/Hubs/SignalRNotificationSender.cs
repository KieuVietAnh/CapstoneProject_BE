using Microsoft.AspNetCore.SignalR;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Hubs;

public class SignalRNotificationSender : IRealtimeNotificationSender
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationSender> _logger;

    public SignalRNotificationSender(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationSender> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendToUserAsync(Guid userId, NotificationDto notification)
    {
        await _hubContext.Clients.User(userId.ToString())
            .SendAsync("NotificationReceived", notification);

        _logger.LogInformation(
            "SignalR event NotificationReceived sent to user {UserId}",
            userId);
    }
}
