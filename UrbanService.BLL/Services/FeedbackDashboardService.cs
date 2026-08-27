using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.Feedback.Dashboard;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class FeedbackDashboardService
    : IFeedbackDashboardService
{
    private const int DefaultMonths = 12;
    private const int MaxMonths = 24;

    private const int DefaultLimit = 10;
    private const int MaxLimit = 100;

    private readonly IUnitOfWork _unitOfWork;

    public FeedbackDashboardService(
        IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FeedbackDashboardOverviewDto>
        GetOverviewAsync(Guid actorUserId)
    {
        var now = DateTime.UtcNow;
        var startOfToday = now.Date;

        var query = await GetScopedFeedbacksAsync(actorUserId);

        var totalFeedback =
            await query.CountAsync();

        var newToday =
            await query.CountAsync(x =>
                x.CreatedAt >= startOfToday &&
                x.CreatedAt <= now);

        var assigned =
            await query.CountAsync(x =>
                x.Status == FeedbackStatus.Assigned);

        var inProgress =
            await query.CountAsync(x =>
                x.Status == FeedbackStatus.InProgress);

        var pendingApproval =
            await query.CountAsync(x =>
                x.Status ==
                FeedbackStatus.SubmittedForApproval);

        var completed =
            await query.CountAsync(x =>
                x.Status == FeedbackStatus.Resolved ||
                x.Status == FeedbackStatus.Approved ||
                x.Status == FeedbackStatus.Closed);

        var cancelled =
            await query.CountAsync(x =>
                x.Status == FeedbackStatus.Cancelled);

        var urgentOpen =
            await query.CountAsync(x =>
                x.Priority == "Urgent" &&
                x.Status != FeedbackStatus.Resolved &&
                x.Status != FeedbackStatus.Approved &&
                x.Status != FeedbackStatus.Closed &&
                x.Status != FeedbackStatus.Cancelled);

        /*
         * Chỉ tính tỷ lệ trên các feedback đã có kết quả cuối.
         */
        var finalizedCount =
            completed + cancelled;

        var completionRate =
            finalizedCount == 0
                ? 0
                : Math.Round(
                    completed /
                    (decimal)finalizedCount *
                    100,
                    2);

        return new FeedbackDashboardOverviewDto
        {
            TotalFeedback = totalFeedback,
            NewToday = newToday,
            Assigned = assigned,
            InProgress = inProgress,
            PendingApproval = pendingApproval,
            Completed = completed,
            Cancelled = cancelled,
            UrgentOpen = urgentOpen,
            CompletionRate = completionRate
        };
    }

    public async Task<List<FeedbackStatusDistributionDto>>
        GetStatusDistributionAsync(Guid actorUserId)
    {
        var query = await GetScopedFeedbacksAsync(actorUserId);

        var total =
            await query.CountAsync();

        if (total == 0)
        {
            return [];
        }

        var data = await query
            .GroupBy(x => x.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return data
            .Select(x =>
                new FeedbackStatusDistributionDto
                {
                    Status = x.Status,

                    Count = x.Count,

                    Percentage = Math.Round(
                        x.Count /
                        (decimal)total *
                        100,
                        2)
                })
            .ToList();
    }

    public async Task<List<FeedbackPriorityDistributionDto>>
        GetPriorityDistributionAsync(Guid actorUserId)
    {
        var query = await GetScopedFeedbacksAsync(actorUserId);

        var total =
            await query.CountAsync();

        if (total == 0)
        {
            return [];
        }

        var data = await query
            .GroupBy(x =>
                x.Priority ?? "Unspecified")
            .Select(group => new
            {
                Priority = group.Key,
                Count = group.Count()
            })
            .ToListAsync();

        var priorityOrder =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Urgent"] = 1,
                ["High"] = 2,
                ["Medium"] = 3,
                ["Low"] = 4,
                ["Unspecified"] = 5
            };

        return data
            .Select(x =>
                new FeedbackPriorityDistributionDto
                {
                    Priority = x.Priority,

                    Count = x.Count,

                    Percentage = Math.Round(
                        x.Count /
                        (decimal)total *
                        100,
                        2)
                })
            .OrderBy(x =>
                priorityOrder.TryGetValue(
                    x.Priority,
                    out var order)
                    ? order
                    : int.MaxValue)
            .ToList();
    }

    public async Task<List<FeedbackCategoryDistributionDto>>
        GetCategoryDistributionAsync(Guid actorUserId)
    {
        var query = await GetScopedFeedbacksAsync(actorUserId);

        var total =
            await query.CountAsync();

        if (total == 0)
        {
            return [];
        }

        var data = await query
            .GroupBy(x => new
            {
                x.CategoryId,

                CategoryName =
                    x.Category != null
                        ? x.Category.CategoryName
                        : "Chưa phân loại"
            })
            .Select(group => new
            {
                group.Key.CategoryId,
                group.Key.CategoryName,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return data
            .Select(x =>
                new FeedbackCategoryDistributionDto
                {
                    CategoryId = x.CategoryId,

                    CategoryName = x.CategoryName,

                    Count = x.Count,

                    Percentage = Math.Round(
                        x.Count /
                        (decimal)total *
                        100,
                        2)
                })
            .ToList();
    }

    public async Task<List<FeedbackAreaDistributionDto>>
        GetAreaDistributionAsync(Guid actorUserId)
    {
        var query = await GetScopedFeedbacksAsync(actorUserId);

        var total =
            await query.CountAsync();

        if (total == 0)
        {
            return [];
        }

        var data = await query
            .GroupBy(x => new
            {
                x.AreaId,
                x.Area.AreaName
            })
            .Select(group => new
            {
                group.Key.AreaId,
                group.Key.AreaName,

                Count =
                    group.Count(),

                CompletedCount =
                    group.Count(x =>
                        x.Status == FeedbackStatus.Resolved ||
                        x.Status == FeedbackStatus.Approved ||
                        x.Status == FeedbackStatus.Closed),

                OpenCount =
                    group.Count(x =>
                        x.Status != FeedbackStatus.Resolved &&
                        x.Status != FeedbackStatus.Approved &&
                        x.Status != FeedbackStatus.Closed &&
                        x.Status != FeedbackStatus.Cancelled)
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return data
            .Select(x =>
                new FeedbackAreaDistributionDto
                {
                    AreaId = x.AreaId,

                    AreaName = x.AreaName,

                    Count = x.Count,

                    OpenCount = x.OpenCount,

                    CompletedCount =
                        x.CompletedCount,

                    Percentage = Math.Round(
                        x.Count /
                        (decimal)total *
                        100,
                        2)
                })
            .ToList();
    }

    public async Task<List<FeedbackMonthlyTrendDto>>
        GetMonthlyTrendAsync(
            Guid actorUserId,
            int months = DefaultMonths)
    {
        months = Math.Clamp(
            months,
            1,
            MaxMonths);

        var now = DateTime.UtcNow;

        var currentMonth =
            new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var fromMonth =
            currentMonth.AddMonths(
                -(months - 1));

        var scopedFeedbacks = await GetScopedFeedbacksAsync(actorUserId);
        var feedbacks = await scopedFeedbacks
            .Where(x =>
                x.CreatedAt >= fromMonth &&
                x.CreatedAt <= now)
            .Select(x => new
            {
                x.CreatedAt,
                x.UpdatedAt,
                x.ApprovedAt,
                x.Status
            })
            .ToListAsync();

        var createdByMonth = feedbacks
            .GroupBy(x => new
            {
                x.CreatedAt.Year,
                x.CreatedAt.Month
            })
            .ToDictionary(
                group =>
                    (
                        group.Key.Year,
                        group.Key.Month
                    ),
                group => group.Count());

        /*
         * Feedback đã hoàn thành được tính theo UpdatedAt,
         * vì dữ liệu seed đã gắn UpdatedAt theo timeline nghiệp vụ.
         */
        var completedByMonth = feedbacks
            .Where(x =>
                (
                    x.Status == FeedbackStatus.Resolved ||
                    x.Status == FeedbackStatus.Approved ||
                    x.Status == FeedbackStatus.Closed
                )
                &&
                x.UpdatedAt.HasValue)
            .GroupBy(x => new
            {
                Year =
                    x.UpdatedAt!.Value.Year,

                Month =
                    x.UpdatedAt.Value.Month
            })
            .ToDictionary(
                group =>
                    (
                        group.Key.Year,
                        group.Key.Month
                    ),
                group => group.Count());

        var cancelledByMonth = feedbacks
            .Where(x =>
                x.Status == FeedbackStatus.Cancelled &&
                x.UpdatedAt.HasValue)
            .GroupBy(x => new
            {
                Year =
                    x.UpdatedAt!.Value.Year,

                Month =
                    x.UpdatedAt.Value.Month
            })
            .ToDictionary(
                group =>
                    (
                        group.Key.Year,
                        group.Key.Month
                    ),
                group => group.Count());

        var result =
            new List<FeedbackMonthlyTrendDto>();

        for (var index = 0;
             index < months;
             index++)
        {
            var month =
                fromMonth.AddMonths(index);

            var key =
                (
                    month.Year,
                    month.Month
                );

            result.Add(
                new FeedbackMonthlyTrendDto
                {
                    Year = month.Year,

                    Month = month.Month,

                    Period =
                        $"{month.Month:00}/{month.Year}",

                    CreatedCount =
                        createdByMonth.TryGetValue(
                            key,
                            out var createdCount)
                            ? createdCount
                            : 0,

                    CompletedCount =
                        completedByMonth.TryGetValue(
                            key,
                            out var completedCount)
                            ? completedCount
                            : 0,

                    CancelledCount =
                        cancelledByMonth.TryGetValue(
                            key,
                            out var cancelledCount)
                            ? cancelledCount
                            : 0
                });
        }

        return result;
    }

    public async Task<List<UrgentOpenFeedbackDto>>
        GetUrgentOpenAsync(
            Guid actorUserId,
            int limit = DefaultLimit)
    {
        limit = Math.Clamp(
            limit,
            1,
            MaxLimit);

        var now =
            DateTime.UtcNow;

        var scopedFeedbacks = await GetScopedFeedbacksAsync(actorUserId);
        var data = await scopedFeedbacks
            .Where(x =>
                x.Priority == "Urgent" &&
                x.Status != FeedbackStatus.Resolved &&
                x.Status != FeedbackStatus.Approved &&
                x.Status != FeedbackStatus.Closed &&
                x.Status != FeedbackStatus.Cancelled)
            .OrderBy(x =>
                x.DueDate ?? DateTime.MaxValue)
            .ThenBy(x =>
                x.CreatedAt)
            .Take(limit)
            .Select(x => new
            {
                x.FeedbackId,
                x.Title,
                x.Status,
                x.Priority,
                x.AreaId,
                AreaName = x.Area.AreaName,
                x.CategoryId,

                CategoryName =
                    x.Category != null
                        ? x.Category.CategoryName
                        : null,

                x.LocationText,
                x.CreatedAt,
                x.DueDate
            })
            .ToListAsync();

        return data
            .Select(x =>
                new UrgentOpenFeedbackDto
                {
                    FeedbackId =
                        x.FeedbackId,

                    Title =
                        x.Title,

                    Status =
                        x.Status,

                    Priority =
                        x.Priority ?? "Urgent",

                    AreaId =
                        x.AreaId,

                    AreaName =
                        x.AreaName,

                    CategoryId =
                        x.CategoryId,

                    CategoryName =
                        x.CategoryName,

                    LocationText =
                        x.LocationText,

                    CreatedAt =
                        x.CreatedAt,

                    DueDate =
                        x.DueDate,

                    AgeHours =
                        Math.Round(
                            Math.Max(
                                0,
                                (
                                    now -
                                    x.CreatedAt
                                ).TotalHours),
                            2),

                    IsOverdue =
                        x.DueDate.HasValue &&
                        x.DueDate.Value < now
                })
            .ToList();
    }

    public async Task<List<RecentFeedbackDto>>
        GetRecentAsync(
            Guid actorUserId,
            int limit = DefaultLimit)
    {
        limit = Math.Clamp(
            limit,
            1,
            MaxLimit);

        var scopedFeedbacks = await GetScopedFeedbacksAsync(actorUserId);
        return await scopedFeedbacks
            .OrderByDescending(x =>
                x.CreatedAt)
            .Take(limit)
            .Select(x =>
                new RecentFeedbackDto
                {
                    FeedbackId =
                        x.FeedbackId,

                    Title =
                        x.Title,

                    Status =
                        x.Status,

                    Priority =
                        x.Priority,

                    AreaId =
                        x.AreaId,

                    AreaName =
                        x.Area.AreaName,

                    CategoryId =
                        x.CategoryId,

                    CategoryName =
                        x.Category != null
                            ? x.Category.CategoryName
                            : null,

                    LocationText =
                        x.LocationText,

                    CreatedAt =
                        x.CreatedAt,

                    UpdatedAt =
                        x.UpdatedAt
                })
            .ToListAsync();
    }

    private async Task<IQueryable<Feedback>> GetScopedFeedbacksAsync(Guid actorUserId)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _unitOfWork,
            actorUserId);
        return ManagementAccessRules.ApplyFeedbackReadScope(
            _unitOfWork.GetRepository<Feedback>().Entities.AsNoTracking(),
            actor);
    }
}
