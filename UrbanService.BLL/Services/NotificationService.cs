using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common.Helpers;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;

    private readonly IUnitOfWork _uow;
    private readonly IRealtimeNotificationSender _realtimeSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork uow,
        IRealtimeNotificationSender realtimeSender,
        ILogger<NotificationService> logger)
    {
        _uow = uow;
        _realtimeSender = realtimeSender;
        _logger = logger;
    }

    public async Task<NotificationDto> SendAsync(
        Guid userId,
        string title,
        string message,
        string type,
        string? targetUrl = null,
        Guid? incidentId = null,
        string? targetType = null,
        string? targetId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID không hợp lệ.",
                nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "Title là bắt buộc.",
                nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Message là bắt buộc.",
                nameof(message));
        }

        var notification = new Notification
        {
            UserId = userId,
            Title = title.Trim(),
            Message = message.Trim(),
            Type = NotificationType.Normalize(type),
            IsRead = false,
            TargetUrl = string.IsNullOrWhiteSpace(targetUrl)
                ? null
                : targetUrl.Trim(),
            IncidentId = incidentId,
            TargetType = string.IsNullOrWhiteSpace(targetType) ? null : targetType.Trim(),
            TargetId = string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim(),
            CreatedAt = SlaDateTimeHelper.UtcNow
        };

        await _uow
            .GetRepository<Notification>()
            .AddAsync(notification);

        await _uow.SaveAsync();

        var dto = Map(notification);

        _logger.LogInformation(
            "Notification {NotificationId} created for {UserId}",
            dto.NotificationId,
            dto.UserId);

        try
        {
            await _realtimeSender.SendToUserAsync(
                userId,
                dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Realtime notification failed.");
        }

        return dto;
    }

    public async Task<PagedResultDto<NotificationDto>>
        GetMyNotificationsAsync(
            Guid userId,
            int pageNumber,
            int pageSize,
            bool? isRead)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID không hợp lệ.",
                nameof(userId));
        }

        pageNumber =
            pageNumber < 1
                ? 1
                : pageNumber;

        pageSize =
            pageSize < 1
                ? 10
                : Math.Min(
                    pageSize,
                    MaxPageSize);

        var query = _uow
            .GetRepository<Notification>()
            .Entities
            .AsNoTracking()
            .Where(n =>
                n.UserId == userId);

        if (isRead.HasValue)
        {
            query = query.Where(n =>
                n.IsRead == isRead.Value);
        }

        var totalItems =
            await query.CountAsync();

        var items =
            await query
                .OrderByDescending(n =>
                    n.CreatedAt)
                .Skip(
                    (pageNumber - 1) *
                    pageSize)
                .Take(pageSize)
                .Select(n =>
                    new NotificationDto
                    {
                        NotificationId =
                            n.NotificationId,

                        UserId =
                            n.UserId,

                        Title =
                            n.Title,

                        Message =
                            n.Message,

                        Type =
                            n.Type,

                        IsRead =
                            n.IsRead,

                        TargetUrl =
                            n.TargetUrl,

                        IncidentId = n.IncidentId,

                        TargetType = n.TargetType,

                        TargetId = n.TargetId,

                        CreatedAt =
                            n.CreatedAt
                    })
                .ToListAsync();

        // SQL Server datetime/datetime2 can return Kind=Unspecified.
        // Mark the clock value as UTC so JSON serialization includes Z.
        foreach (var item in items)
        {
            item.CreatedAt =
                SlaDateTimeHelper.AsUtc(
                    item.CreatedAt);
        }

        return new PagedResultDto<NotificationDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages =
                totalItems == 0
                    ? 0
                    : (int)Math.Ceiling(
                        totalItems /
                        (double)pageSize)
        };
    }

    public async Task MarkAsReadAsync(
        Guid userId,
        int notificationId)
    {
        var notification = await _uow
            .GetRepository<Notification>()
            .Entities
            .FirstOrDefaultAsync(n =>
                n.NotificationId ==
                    notificationId &&
                n.UserId == userId);

        if (notification == null)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy notification.");
        }

        if (notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        notification.UpdatedAt =
            SlaDateTimeHelper.UtcNow;

        await _uow.SaveAsync();
    }

    public async Task MarkAllAsReadAsync(
        Guid userId)
    {
        var notifications = await _uow
            .GetRepository<Notification>()
            .Entities
            .Where(n =>
                n.UserId == userId &&
                !n.IsRead)
            .ToListAsync();

        if (notifications.Count == 0)
        {
            return;
        }

        var now =
            SlaDateTimeHelper.UtcNow;

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.UpdatedAt = now;
        }

        await _uow.SaveAsync();
    }

    private static NotificationDto Map(
        Notification notification)
    {
        return new NotificationDto
        {
            NotificationId =
                notification.NotificationId,

            UserId =
                notification.UserId,

            Title =
                notification.Title,

            Message =
                notification.Message,

            Type =
                notification.Type,

            IsRead =
                notification.IsRead,

            TargetUrl =
                notification.TargetUrl,

            IncidentId = notification.IncidentId,

            TargetType = notification.TargetType,

            TargetId = notification.TargetId,

            CreatedAt =
                SlaDateTimeHelper.AsUtc(
                    notification.CreatedAt)
        };
    }
}
