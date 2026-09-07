using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.Incident.Dashboard;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

/// <summary>
/// Dashboard vận hành sự vụ đô thị.
///
/// Dashboard đọc từ hai đơn vị khác nhau và không được trộn lẫn:
/// - Chỉ số tiếp nhận đếm <see cref="Feedback"/> vì đó là lượng phản ánh
///   người dân thực sự gửi lên.
/// - Chỉ số xử lý đếm <see cref="Incident"/> vì trạng thái, phân công và SLA
///   đều thuộc sự vụ. Nhiều phản ánh trùng nhau chỉ tạo ra một việc phải xử lý.
/// </summary>
public class IncidentDashboardService
    : IIncidentDashboardService
{
    private const int DefaultMonths = 12;
    private const int MaxMonths = 24;

    private const int DefaultLimit = 10;
    private const int MaxLimit = 100;

    private const int DefaultMapPointsPerArea = 500;
    private const int MaxMapPointsPerArea = 5000;

    private static readonly string[] ClosedIncidentStatuses =
    [
        IncidentStatus.Resolved,
        IncidentStatus.Approved,
        IncidentStatus.Closed
    ];

    private readonly IUnitOfWork _unitOfWork;

    public IncidentDashboardService(
        IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IncidentDashboardOverviewDto>
        GetOverviewAsync(Guid actorUserId)
    {
        var now = DateTime.UtcNow;
        var startOfToday = now.Date;

        var reports = await GetScopedFeedbacksAsync(actorUserId);
        var incidents = await GetScopedIncidentsAsync(actorUserId);

        var totalFeedback =
            await reports.CountAsync();

        var newToday =
            await reports.CountAsync(x =>
                x.CreatedAt >= startOfToday &&
                x.CreatedAt <= now);

        var totalIncident =
            await incidents.CountAsync();

        var newIncidentToday =
            await incidents.CountAsync(x =>
                x.CreatedAt >= startOfToday &&
                x.CreatedAt <= now);

        var assigned =
            await incidents.CountAsync(x =>
                x.Status == IncidentStatus.Assigned);

        var inProgress =
            await incidents.CountAsync(x =>
                x.Status == IncidentStatus.InProgress);

        var pendingApproval =
            await incidents.CountAsync(x =>
                x.Status ==
                IncidentStatus.SubmittedForApproval);

        var completed =
            await incidents.CountAsync(x =>
                ClosedIncidentStatuses.Contains(x.Status));

        var cancelled =
            await incidents.CountAsync(x =>
                x.Status == IncidentStatus.Cancelled ||
                x.Status == IncidentStatus.Rejected);

        var urgentOpen =
            await incidents.CountAsync(x =>
                x.Priority == "Urgent" &&
                !ClosedIncidentStatuses.Contains(x.Status) &&
                x.Status != IncidentStatus.Cancelled &&
                x.Status != IncidentStatus.Rejected);

        /*
         * Chỉ tính tỷ lệ trên các sự vụ đã có kết quả cuối.
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

        return new IncidentDashboardOverviewDto
        {
            TotalFeedback = totalFeedback,
            NewToday = newToday,
            TotalIncident = totalIncident,
            NewIncidentToday = newIncidentToday,
            Assigned = assigned,
            InProgress = inProgress,
            PendingApproval = pendingApproval,
            Completed = completed,
            Cancelled = cancelled,
            UrgentOpen = urgentOpen,
            CompletionRate = completionRate
        };
    }

    public async Task<List<IncidentStatusDistributionDto>>
        GetStatusDistributionAsync(Guid actorUserId)
    {
        var query = await GetScopedIncidentsAsync(actorUserId);

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
                new IncidentStatusDistributionDto
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

    public async Task<List<IncidentPriorityDistributionDto>>
        GetPriorityDistributionAsync(Guid actorUserId)
    {
        var query = await GetScopedIncidentsAsync(actorUserId);

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
                new IncidentPriorityDistributionDto
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

    public async Task<List<IncidentCategoryDistributionDto>>
        GetCategoryDistributionAsync(Guid actorUserId)
    {
        var query = await GetScopedIncidentsAsync(actorUserId);

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
                new IncidentCategoryDistributionDto
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

    public async Task<List<IncidentAreaDistributionDto>>
        GetAreaDistributionAsync(
            Guid actorUserId,
            int maxPointsPerArea = DefaultMapPointsPerArea)
    {
        maxPointsPerArea = Math.Clamp(
            maxPointsPerArea,
            1,
            MaxMapPointsPerArea);

        var query = await GetScopedIncidentsAsync(actorUserId);

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
                x.Area.AreaName,
                x.Area.CenterLatitude,
                x.Area.CenterLongitude
            })
            .Select(group => new
            {
                group.Key.AreaId,
                group.Key.AreaName,
                group.Key.CenterLatitude,
                group.Key.CenterLongitude,

                Count =
                    group.Count(),

                CompletedCount =
                    group.Count(x =>
                        ClosedIncidentStatuses.Contains(x.Status)),

                OpenCount =
                    group.Count(x =>
                        !ClosedIncidentStatuses.Contains(x.Status) &&
                        x.Status != IncidentStatus.Cancelled &&
                        x.Status != IncidentStatus.Rejected)
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var pointsByArea =
            await GetMapPointsByAreaAsync(query);

        return data
            .Select(x =>
            {
                var hasPoints = pointsByArea.TryGetValue(
                    x.AreaId,
                    out var areaPoints);

                return new IncidentAreaDistributionDto
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
                        2),

                    CenterLatitude = x.CenterLatitude,

                    CenterLongitude = x.CenterLongitude,

                    MappedCount =
                        hasPoints
                            ? areaPoints!.Count
                            : 0,

                    Points =
                        hasPoints
                            ? areaPoints!
                                .Take(maxPointsPerArea)
                                .ToList()
                            : []
                };
            })
            .ToList();
    }

    /// <summary>
    /// Lấy tọa độ các sự vụ trong phạm vi đọc của người dùng, gom theo khu vực.
    ///
    /// Việc lọc theo phạm vi và theo điều kiện có tọa độ được thực hiện ở database;
    /// chỉ thao tác gom nhóm và cắt theo giới hạn mới chạy trong bộ nhớ, nên số dòng
    /// đọc lên đúng bằng số điểm thực sự vẽ được trên bản đồ.
    /// </summary>
    private static async Task<Dictionary<int, List<IncidentMapPointDto>>>
        GetMapPointsByAreaAsync(IQueryable<Incident> scopedIncidents)
    {
        var rows = await scopedIncidents
            .Where(incident =>
                incident.Latitude.HasValue &&
                incident.Longitude.HasValue)
            .OrderByDescending(incident => incident.CreatedAt)
            .Select(incident => new
            {
                incident.AreaId,
                incident.IncidentId,
                incident.Title,
                incident.Latitude,
                incident.Longitude,
                incident.Status,
                incident.Priority,
                incident.Severity,
                incident.CategoryId,

                CategoryName =
                    incident.Category != null
                        ? incident.Category.CategoryName
                        : null,

                incident.LocationText,

                ReportCount = incident.IncidentReportLinks.Count(link =>
                    link.LinkStatus == IncidentLinkStatus.Active),

                incident.CreatedAt
            })
            .ToListAsync();

        return rows
            .GroupBy(row => row.AreaId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(row => new IncidentMapPointDto
                    {
                        IncidentId = row.IncidentId,

                        Title = row.Title,

                        Latitude = row.Latitude!.Value,

                        Longitude = row.Longitude!.Value,

                        Status = row.Status,

                        Priority = row.Priority,

                        Severity = row.Severity,

                        CategoryId = row.CategoryId,

                        CategoryName = row.CategoryName,

                        LocationText = row.LocationText,

                        ReportCount = row.ReportCount,

                        IsOpen = IsOpenIncidentStatus(row.Status),

                        CreatedAt = row.CreatedAt
                    })
                    .ToList());
    }

    public async Task<List<IncidentMonthlyTrendDto>>
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

        /*
         * Lượng tiếp nhận đếm theo Report, còn tiến độ xử lý đếm theo Incident.
         * Hai đường trên cùng biểu đồ nhưng khác đơn vị nên phải tách rõ.
         */
        var scopedReports = await GetScopedFeedbacksAsync(actorUserId);
        var reportMonths = await scopedReports
            .Where(x =>
                x.CreatedAt >= fromMonth &&
                x.CreatedAt <= now)
            .Select(x => x.CreatedAt)
            .ToListAsync();

        var scopedIncidents = await GetScopedIncidentsAsync(actorUserId);
        var incidents = await scopedIncidents
            .Where(x =>
                x.CreatedAt >= fromMonth &&
                x.CreatedAt <= now)
            .Select(x => new
            {
                x.CreatedAt,
                x.UpdatedAt,
                x.ResolvedAt,
                x.ClosedAt,
                x.Status
            })
            .ToListAsync();

        var reportsByMonth = reportMonths
            .GroupBy(createdAt => new
            {
                createdAt.Year,
                createdAt.Month
            })
            .ToDictionary(
                group =>
                    (
                        group.Key.Year,
                        group.Key.Month
                    ),
                group => group.Count());

        var createdByMonth = incidents
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
         * Sự vụ đã hoàn thành được tính theo mốc kết thúc thực tế:
         * ClosedAt, rồi tới ResolvedAt, cuối cùng mới dùng UpdatedAt.
         */
        var completedByMonth = incidents
            .Where(x =>
                ClosedIncidentStatuses.Contains(x.Status))
            .Select(x => x.ClosedAt ?? x.ResolvedAt ?? x.UpdatedAt)
            .Where(completedAt => completedAt.HasValue)
            .GroupBy(completedAt => new
            {
                completedAt!.Value.Year,
                completedAt.Value.Month
            })
            .ToDictionary(
                group =>
                    (
                        group.Key.Year,
                        group.Key.Month
                    ),
                group => group.Count());

        var cancelledByMonth = incidents
            .Where(x =>
                (x.Status == IncidentStatus.Cancelled ||
                 x.Status == IncidentStatus.Rejected) &&
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
            new List<IncidentMonthlyTrendDto>();

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
                new IncidentMonthlyTrendDto
                {
                    Year = month.Year,

                    Month = month.Month,

                    Period =
                        $"{month.Month:00}/{month.Year}",

                    ReportCount =
                        reportsByMonth.TryGetValue(
                            key,
                            out var reportCount)
                            ? reportCount
                            : 0,

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

    public async Task<List<UrgentOpenIncidentDto>>
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

        var scopedIncidents = await GetScopedIncidentsAsync(actorUserId);
        var data = await scopedIncidents
            .Where(x =>
                x.Priority == "Urgent" &&
                !ClosedIncidentStatuses.Contains(x.Status) &&
                x.Status != IncidentStatus.Cancelled &&
                x.Status != IncidentStatus.Rejected)
            .OrderBy(x =>
                x.DueDate ?? DateTime.MaxValue)
            .ThenBy(x =>
                x.CreatedAt)
            .Take(limit)
            .Select(x => new
            {
                x.IncidentId,
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
                x.DueDate,

                ReportCount = x.IncidentReportLinks.Count(link =>
                    link.LinkStatus == IncidentLinkStatus.Active)
            })
            .ToListAsync();

        return data
            .Select(x =>
                new UrgentOpenIncidentDto
                {
                    IncidentId =
                        x.IncidentId,

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

                    ReportCount =
                        x.ReportCount,

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

    /// <summary>
    /// Danh sách phản ánh vừa tiếp nhận. Đây là chỉ số tiếp nhận nên vẫn
    /// đọc theo Report để nhân sự thấy đúng những gì người dân mới gửi.
    /// </summary>
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

    /// <summary>
    /// Sự vụ đang mở là sự vụ chưa kết thúc và chưa bị hủy hoặc từ chối.
    /// </summary>
    private static bool IsOpenIncidentStatus(string status)
    {
        return !ClosedIncidentStatuses.Contains(status) &&
            status != IncidentStatus.Cancelled &&
            status != IncidentStatus.Rejected;
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

    /// <summary>
    /// Sự vụ đã gộp không được đếm riêng, vì công việc của chúng đã chuyển
    /// sang sự vụ canonical.
    /// </summary>
    private async Task<IQueryable<Incident>> GetScopedIncidentsAsync(Guid actorUserId)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _unitOfWork,
            actorUserId);
        return ManagementAccessRules.ApplyIncidentReadScope(
            _unitOfWork.GetRepository<Incident>().Entities.AsNoTracking(),
            actor)
            .Where(incident => incident.MergedIntoIncidentId == null);
    }
}
