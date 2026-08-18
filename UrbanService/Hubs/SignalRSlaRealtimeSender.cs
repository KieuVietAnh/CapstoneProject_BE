using Microsoft.AspNetCore.SignalR;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Hubs;

public class SignalRSlaRealtimeSender
    : ISlaRealtimeSender
{
    private readonly IHubContext<SlaHub>
        _hubContext;

    private readonly ILogger<SignalRSlaRealtimeSender>
        _logger;

    public SignalRSlaRealtimeSender(
        IHubContext<SlaHub> hubContext,
        ILogger<SignalRSlaRealtimeSender> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendSlaUpdatedAsync(
        Guid feedbackId,
        long feedbackSlaId,
        string eventType)
    {
        await _hubContext
            .Clients
            .All
            .SendAsync(
                "SlaUpdated",
                new
                {
                    FeedbackId = feedbackId,
                    FeedbackSlaId = feedbackSlaId,
                    EventType = eventType
                });

        _logger.LogInformation(
            "SignalR event SlaUpdated sent. " +
            "FeedbackId: {FeedbackId}, " +
            "FeedbackSlaId: {FeedbackSlaId}, " +
            "EventType: {EventType}",
            feedbackId,
            feedbackSlaId,
            eventType);
    }
}