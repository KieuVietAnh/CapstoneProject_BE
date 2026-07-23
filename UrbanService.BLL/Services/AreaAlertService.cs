using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Data;
using UrbanService.DAL.Entities;

namespace UrbanService.BLL.Services;

public class AreaAlertService : IAreaAlertService
{
    private readonly UrbanServiceDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public AreaAlertService(
        UrbanServiceDbContext dbContext,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<UserAreaAlertDto> CreateManualAlertAsync(Guid createdByUserId, CreateAreaAlertRequest request)
    {
        ValidateAlertRequest(request.Title, request.Message, request.Severity, request.StartAt, request.EndAt);

        await EnsureAreaExistsAsync(request.AreaId);
        await EnsureCategoryExistsAsync(request.CategoryId);
        await EnsureHotspotExistsAsync(request.HotspotId);

        var alert = new AreaAlert
        {
            AreaId = request.AreaId,
            CreatedByUserId = createdByUserId,
            CategoryId = request.CategoryId,
            HotspotId = request.HotspotId,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            AlertType = string.IsNullOrWhiteSpace(request.AlertType) ? "Manual" : request.AlertType.Trim(),
            Severity = request.Severity.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RadiusMeters = request.RadiusMeters,
            Status = GetStatus(request.StartAt, request.EndAt),
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AreaAlerts.Add(alert);
        await _dbContext.SaveChangesAsync();

        await NotifySubscribedUsersAsync(alert);

        return await GetAlertDtoAsync(alert.AlertId, createdByUserId);
    }

    public async Task<UserAreaAlertDto> CreateAlertFromFeedbackAsync(
        Guid createdByUserId,
        Guid feedbackId,
        CreateAreaAlertFromFeedbackRequest request)
    {
        ValidateAlertRequest(request.Title, request.Message, request.Severity, request.StartAt, request.EndAt);

        var feedback = await _dbContext.Feedbacks
            .Include(f => f.AnalysisResults)
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId);

        if (feedback == null)
        {
            throw new KeyNotFoundException("Feedback not found.");
        }

        var latestAnalysis = feedback.AnalysisResults
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (latestAnalysis != null &&
            !string.Equals(latestAnalysis.UrgencyLevel, "High", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(latestAnalysis.UrgencyLevel, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only feedback with High or Critical urgency can create an area alert.");
        }

        var alert = new AreaAlert
        {
            AreaId = feedback.AreaId,
            CreatedByUserId = createdByUserId,
            CategoryId = feedback.CategoryId,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            AlertType = "FeedbackCritical",
            Severity = request.Severity.Trim(),
            Latitude = feedback.Latitude,
            Longitude = feedback.Longitude,
            RadiusMeters = request.RadiusMeters,
            Status = GetStatus(request.StartAt, request.EndAt),
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AreaAlerts.Add(alert);
        await _dbContext.SaveChangesAsync();

        await NotifySubscribedUsersAsync(alert);

        var dto = await GetAlertDtoAsync(alert.AlertId, createdByUserId);
        dto.SourceFeedbackId = feedbackId;
        return dto;
    }

    public async Task<PagedResultDto<UserAreaAlertDto>> GetAlertsAsync(Guid userId, AreaAlertQueryParameters query)
    {
        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var now = DateTime.UtcNow;

        var subscribedAreaIdsQuery = _dbContext.UserAreaSubscriptions
            .Where(s => s.UserId == userId && s.ReceiveAlerts)
            .Select(s => s.AreaId);

        IQueryable<AreaAlert> alertsQuery = _dbContext.AreaAlerts
            .Include(a => a.Area)
            .Include(a => a.Category)
            .Include(a => a.Hotspot);

        if (query.OnlySubscribedAreas)
        {
            alertsQuery = alertsQuery.Where(a => subscribedAreaIdsQuery.Contains(a.AreaId));
        }

        if (query.AreaId.HasValue)
        {
            alertsQuery = alertsQuery.Where(a => a.AreaId == query.AreaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (string.Equals(query.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                alertsQuery = alertsQuery.Where(a =>
                    a.StartAt <= now &&
                    (a.EndAt == null || a.EndAt >= now) &&
                    !string.Equals(a.Status, "Cancelled"));
            }
            else
            {
                alertsQuery = alertsQuery.Where(a => a.Status == query.Status);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            alertsQuery = alertsQuery.Where(a => a.Severity == query.Severity);
        }

        var totalItems = await alertsQuery.CountAsync();
        var subscribedAreaIds = await subscribedAreaIdsQuery.ToListAsync();

        var alerts = await alertsQuery
            .OrderByDescending(a => a.StartAt)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<UserAreaAlertDto>
        {
            Items = alerts.Select(a => MapAlert(a, subscribedAreaIds.Contains(a.AreaId))).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<IReadOnlyCollection<UserAreaSubscriptionDto>> GetMySubscriptionsAsync(Guid userId)
    {
        return await _dbContext.UserAreaSubscriptions
            .Include(s => s.Area)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.IsPrimaryArea)
            .ThenBy(s => s.Area.AreaName)
            .Select(s => new UserAreaSubscriptionDto
            {
                SubscriptionId = s.SubscriptionId,
                UserId = s.UserId,
                AreaId = s.AreaId,
                AreaName = s.Area.AreaName,
                IsPrimaryArea = s.IsPrimaryArea,
                ReceiveAlerts = s.ReceiveAlerts,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<UserAreaSubscriptionDto> CreateSubscriptionAsync(Guid userId, CreateAreaSubscriptionRequest request)
    {
        await EnsureAreaExistsAsync(request.AreaId);

        var existing = await _dbContext.UserAreaSubscriptions
            .Include(s => s.Area)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AreaId == request.AreaId);

        if (request.IsPrimaryArea)
        {
            await _dbContext.UserAreaSubscriptions
                .Where(s => s.UserId == userId && s.IsPrimaryArea)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.IsPrimaryArea, false));
        }

        if (existing != null)
        {
            existing.IsPrimaryArea = request.IsPrimaryArea;
            existing.ReceiveAlerts = request.ReceiveAlerts;
            await _dbContext.SaveChangesAsync();

            return MapSubscription(existing);
        }

        var subscription = new UserAreaSubscription
        {
            UserId = userId,
            AreaId = request.AreaId,
            IsPrimaryArea = request.IsPrimaryArea,
            ReceiveAlerts = request.ReceiveAlerts,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.UserAreaSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        subscription = await _dbContext.UserAreaSubscriptions
            .Include(s => s.Area)
            .FirstAsync(s => s.SubscriptionId == subscription.SubscriptionId);

        return MapSubscription(subscription);
    }

    public async Task DeleteSubscriptionAsync(Guid userId, int areaId)
    {
        var subscription = await _dbContext.UserAreaSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AreaId == areaId);

        if (subscription == null)
        {
            throw new KeyNotFoundException("Area subscription not found.");
        }

        _dbContext.UserAreaSubscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync();
    }

    private async Task NotifySubscribedUsersAsync(AreaAlert alert)
    {
        var userIds = await _dbContext.UserAreaSubscriptions
            .Where(s => s.AreaId == alert.AreaId && s.ReceiveAlerts)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var userId in userIds)
        {
            await _notificationService.SendAsync(
                userId,
                alert.Title,
                alert.Message,
                "AreaAlert",
                $"/area-alerts/{alert.AlertId}");
        }
    }

    private async Task<UserAreaAlertDto> GetAlertDtoAsync(int alertId, Guid userId)
    {
        var alert = await _dbContext.AreaAlerts
            .Include(a => a.Area)
            .Include(a => a.Category)
            .Include(a => a.Hotspot)
            .FirstOrDefaultAsync(a => a.AlertId == alertId);

        if (alert == null)
        {
            throw new KeyNotFoundException("Area alert not found.");
        }

        var isSubscribed = await _dbContext.UserAreaSubscriptions
            .AnyAsync(s => s.UserId == userId && s.AreaId == alert.AreaId && s.ReceiveAlerts);

        return MapAlert(alert, isSubscribed);
    }

    private static UserAreaAlertDto MapAlert(AreaAlert alert, bool isSubscribedArea)
    {
        return new UserAreaAlertDto
        {
            AlertId = alert.AlertId,
            AreaId = alert.AreaId,
            AreaName = alert.Area.AreaName,
            CreatedByUserId = alert.CreatedByUserId,
            CategoryId = alert.CategoryId,
            CategoryName = alert.Category?.CategoryName,
            HotspotId = alert.HotspotId,
            HotspotName = alert.Hotspot == null ? null : $"Hotspot #{alert.Hotspot.HotspotId}",
            Title = alert.Title,
            Message = alert.Message,
            AlertType = alert.AlertType,
            Severity = alert.Severity,
            Latitude = alert.Latitude,
            Longitude = alert.Longitude,
            RadiusMeters = alert.RadiusMeters,
            Status = alert.Status,
            StartAt = alert.StartAt,
            EndAt = alert.EndAt,
            CreatedAt = alert.CreatedAt,
            UpdatedAt = alert.UpdatedAt,
            IsSubscribedArea = isSubscribedArea
        };
    }

    private static UserAreaSubscriptionDto MapSubscription(UserAreaSubscription subscription)
    {
        return new UserAreaSubscriptionDto
        {
            SubscriptionId = subscription.SubscriptionId,
            UserId = subscription.UserId,
            AreaId = subscription.AreaId,
            AreaName = subscription.Area.AreaName,
            IsPrimaryArea = subscription.IsPrimaryArea,
            ReceiveAlerts = subscription.ReceiveAlerts,
            CreatedAt = subscription.CreatedAt
        };
    }

    private async Task EnsureAreaExistsAsync(int areaId)
    {
        var exists = await _dbContext.OperatingAreas.AnyAsync(a => a.AreaId == areaId && a.IsActive);
        if (!exists)
        {
            throw new KeyNotFoundException("Area not found or inactive.");
        }
    }

    private async Task EnsureCategoryExistsAsync(int? categoryId)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await _dbContext.UrbanServiceCategories.AnyAsync(c => c.CategoryId == categoryId.Value);
        if (!exists)
        {
            throw new KeyNotFoundException("Category not found.");
        }
    }

    private async Task EnsureHotspotExistsAsync(int? hotspotId)
    {
        if (!hotspotId.HasValue)
        {
            return;
        }

        var exists = await _dbContext.AreaHotspots.AnyAsync(h => h.HotspotId == hotspotId.Value);
        if (!exists)
        {
            throw new KeyNotFoundException("Hotspot not found.");
        }
    }

    private static void ValidateAlertRequest(
        string title,
        string message,
        string severity,
        DateTime startAt,
        DateTime? endAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is required.");
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Severity is required.");
        }

        if (endAt.HasValue && endAt.Value <= startAt)
        {
            throw new ArgumentException("EndAt must be greater than StartAt.");
        }
    }

    private static string GetStatus(DateTime startAt, DateTime? endAt)
    {
        var now = DateTime.UtcNow;

        if (endAt.HasValue && endAt.Value < now)
        {
            return "Expired";
        }

        return startAt <= now ? "Active" : "Scheduled";
    }
}