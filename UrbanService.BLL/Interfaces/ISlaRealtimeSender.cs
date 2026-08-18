namespace UrbanService.BLL.Interfaces;

public interface ISlaRealtimeSender
{
    Task SendSlaUpdatedAsync(
        Guid feedbackId,
        long feedbackSlaId,
        string eventType);
}