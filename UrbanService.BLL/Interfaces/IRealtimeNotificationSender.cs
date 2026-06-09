using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces;

public interface IRealtimeNotificationSender
{
    Task SendToUserAsync(Guid userId, NotificationDto notification);
}
