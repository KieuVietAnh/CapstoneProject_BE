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
        Guid incidentId,
        long incidentSlaId,
        string eventType)
    {
        await _hubContext
            .Clients
            .All
            .SendAsync(
                "SlaUpdated",
                new
                {
                    IncidentId = incidentId,
                    IncidentSlaId = incidentSlaId,
                    EventType = eventType
                });

        _logger.LogInformation(
            "SignalR event SlaUpdated sent. " +
            "IncidentId: {IncidentId}, " +
            "IncidentSlaId: {IncidentSlaId}, " +
            "EventType: {EventType}",
            incidentId,
            incidentSlaId,
            eventType);
    }
}