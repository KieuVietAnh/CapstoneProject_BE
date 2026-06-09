using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using UrbanService.Hubs;

namespace UrbanService.Services;

public class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork uow,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _uow = uow;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<NotificationDto> SendAsync(
        Guid userId,
        string title,
        string message,
        string type,
        string? targetUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = NotificationType.Normalize(type),
            IsRead = false,
            TargetUrl = targetUrl,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<Notification>().AddAsync(notification);
        await _uow.SaveAsync();

        var dto = Map(notification);
        _logger.LogInformation(
            "Notification {NotificationId} saved for user {UserId} with type {Type}",
            dto.NotificationId,
            dto.UserId,
            dto.Type);

        await _hubContext.Clients.User(userId.ToString()).SendAsync("NotificationReceived", dto);
        _logger.LogInformation(
            "SignalR event NotificationReceived sent to user {UserId}",
            dto.UserId);

        return dto;
    }

    public async Task<PagedResultDto<NotificationDto>> GetMyNotificationsAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        bool? isRead)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? 10 : Math.Min(pageSize, MaxPageSize);

        var query = _uow.GetRepository<Notification>().Entities
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                TargetUrl = n.TargetUrl,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return new PagedResultDto<NotificationDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task MarkAsReadAsync(Guid userId, int notificationId)
    {
        var notification = await _uow.GetRepository<Notification>().Entities
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

        if (notification == null)
        {
            throw new Exception("Không tìm thấy notification.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveAsync();
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        var notifications = await _uow.GetRepository<Notification>().Entities
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (notifications.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.UpdatedAt = now;
        }

        await _uow.SaveAsync();
    }

    private static NotificationDto Map(Notification notification)
    {
        return new NotificationDto
        {
            NotificationId = notification.NotificationId,
            UserId = notification.UserId,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            IsRead = notification.IsRead,
            TargetUrl = notification.TargetUrl,
            CreatedAt = notification.CreatedAt
        };
    }
}
