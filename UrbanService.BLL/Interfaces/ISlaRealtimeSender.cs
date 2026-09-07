namespace UrbanService.BLL.Interfaces;

public interface ISlaRealtimeSender
{
    Task SendSlaUpdatedAsync(
        Guid incidentId,
        long incidentSlaId,
        string eventType);
}