using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces;

public interface INotificationService
{
    Task<NotificationDto> SendAsync(
        Guid userId,
        string title,
        string message,
        string type,
        string? targetUrl = null);

    Task<PagedResultDto<NotificationDto>> GetMyNotificationsAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        bool? isRead);

    Task MarkAsReadAsync(Guid userId, int notificationId);

    Task MarkAllAsReadAsync(Guid userId);
}
