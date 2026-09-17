using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common.Helpers;
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
        var now = SlaDateTimeHelper.UtcNow;

        /*
         * Ranh giới ngày phải tính theo giờ Việt Nam. Nếu dùng now.Date của UTC
         * thì mọi bản ghi từ 00:00 đến 07:00 giờ Việt Nam bị đẩy sang hôm trước.
         */
        var startOfToday =
            SlaDateTimeHelper.VietnamToUtc(
                SlaDateTimeHelper.ToVietnamTime(now).Date);

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

        var pointsByArea = (await GetMapPointsAsync(query))
            .GroupBy(point => point.AreaId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

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
    /// Phân bố sự vụ theo danh mục, tách tiếp theo từng phường, kèm tọa độ.
    ///
    /// Mỗi sự vụ được quy về đúng một ô (danh mục, phường) nên tổng số đếm của
    /// các ô bằng tổng số sự vụ; có thể cộng dồn mà không sợ đếm trùng.
    /// </summary>
    public async Task<List<IncidentCategoryAreaDistributionDto>>
        GetCategoryAreaDistributionAsync(
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

        var cells = await query
            .GroupBy(x => new
            {
                x.CategoryId,

                CategoryName =
                    x.Category != null
                        ? x.Category.CategoryName
                        : "Chưa phân loại",

                x.AreaId,
                x.Area.AreaName,
                x.Area.WardCode,
                x.Area.DistrictName,
                x.Area.CenterLatitude,
                x.Area.CenterLongitude
            })
            .Select(group => new
            {
                group.Key.CategoryId,
                group.Key.CategoryName,
                group.Key.AreaId,
                group.Key.AreaName,
                group.Key.WardCode,
                group.Key.DistrictName,
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
            .ToListAsync();

        /*
         * Gom điểm theo cặp (danh mục, phường) để mỗi ô chỉ nhận đúng các sự vụ
         * của mình, thay vì nhận toàn bộ điểm của phường.
         */
        var pointsByCell = (await GetMapPointsAsync(query))
            .GroupBy(point => new
            {
                point.CategoryId,
                point.AreaId
            })
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        return cells
            .GroupBy(cell => new
            {
                cell.CategoryId,
                cell.CategoryName
            })
            .Select(categoryGroup =>
            {
                var categoryCount =
                    categoryGroup.Sum(cell => cell.Count);

                var areas = categoryGroup
                    .OrderByDescending(cell => cell.Count)
                    .Select(cell =>
                    {
                        var hasPoints = pointsByCell.TryGetValue(
                            new
                            {
                                categoryGroup.Key.CategoryId,
                                cell.AreaId
                            },
                            out var cellPoints);

                        return new IncidentAreaBreakdownDto
                        {
                            AreaId = cell.AreaId,

                            AreaName = cell.AreaName,

                            WardCode = cell.WardCode,

                            DistrictName = cell.DistrictName,

                            CenterLatitude = cell.CenterLatitude,

                            CenterLongitude = cell.CenterLongitude,

                            Count = cell.Count,

                            OpenCount = cell.OpenCount,

                            CompletedCount = cell.CompletedCount,

                            PercentageInCategory =
                                ToPercentage(
                                    cell.Count,
                                    categoryCount),

                            MappedCount =
                                hasPoints
                                    ? cellPoints!.Count
                                    : 0,

                            Points =
                                hasPoints
                                    ? cellPoints!
                                        .Take(maxPointsPerArea)
                                        .ToList()
                                    : []
                        };
                    })
                    .ToList();

                return new IncidentCategoryAreaDistributionDto
                {
                    CategoryId = categoryGroup.Key.CategoryId,

                    CategoryName = categoryGroup.Key.CategoryName,

                    Count = categoryCount,

                    OpenCount =
                        categoryGroup.Sum(cell => cell.OpenCount),

                    CompletedCount =
                        categoryGroup.Sum(cell => cell.CompletedCount),

                    Percentage =
                        ToPercentage(categoryCount, total),

                    MappedCount =
                        areas.Sum(area => area.MappedCount),

                    Areas = areas
                };
            })
            .OrderByDescending(x => x.Count)
            .ToList();
    }

    /// <summary>
    /// Tình hình tiếp nhận và xử lý trong ngày hôm nay, ranh giới ngày tính theo
    /// giờ Việt Nam.
    /// </summary>
    public async Task<IncidentTodaySummaryDto>
        GetTodaySummaryAsync(Guid actorUserId)
    {
        var nowUtc =
            SlaDateTimeHelper.UtcNow;

        var vietnamNow =
            SlaDateTimeHelper.ToVietnamTime(nowUtc);

        var startOfDayUtc =
            SlaDateTimeHelper.VietnamToUtc(
                vietnamNow.Date);

        var endOfDayUtc =
            SlaDateTimeHelper.VietnamToUtc(
                vietnamNow.Date.AddDays(1));

        var reports = await GetScopedFeedbacksAsync(actorUserId);
        var incidents = await GetScopedIncidentsAsync(actorUserId);

        var reportsToday = reports
            .Where(x =>
                x.CreatedAt >= startOfDayUtc &&
                x.CreatedAt < endOfDayUtc);

        var reportCount =
            await reportsToday.CountAsync();

        var incidentCount =
            await incidents.CountAsync(x =>
                x.CreatedAt >= startOfDayUtc &&
                x.CreatedAt < endOfDayUtc);

        /*
         * Đếm theo thời điểm sự vụ được xử lý xong, không theo trạng thái hiện
         * tại, để con số phản ánh đúng khối lượng giải quyết trong hôm nay.
         */
        var resolvedCount =
            await incidents.CountAsync(x =>
                x.ResolvedAt.HasValue &&
                x.ResolvedAt.Value >= startOfDayUtc &&
                x.ResolvedAt.Value < endOfDayUtc);

        var byCategory = await reportsToday
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

        var byArea = await reportsToday
            .GroupBy(x => new
            {
                x.AreaId,
                x.Area.AreaName,
                x.Area.WardCode,
                x.Area.DistrictName
            })
            .Select(group => new
            {
                group.Key.AreaId,
                group.Key.AreaName,
                group.Key.WardCode,
                group.Key.DistrictName,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return new IncidentTodaySummaryDto
        {
            Date = DateOnly.FromDateTime(vietnamNow),

            StartOfDayUtc = startOfDayUtc,

            EndOfDayUtc = endOfDayUtc,

            ReportCount = reportCount,

            IncidentCount = incidentCount,

            ResolvedCount = resolvedCount,

            ByCategory = byCategory
                .Select(x => new IncidentCategoryDistributionDto
                {
                    CategoryId = x.CategoryId,

                    CategoryName = x.CategoryName,

                    Count = x.Count,

                    Percentage =
                        ToPercentage(x.Count, reportCount)
                })
                .ToList(),

            ByArea = byArea
                .Select(x => new IncidentAreaCountDto
                {
                    AreaId = x.AreaId,

                    AreaName = x.AreaName,

                    WardCode = x.WardCode,

                    DistrictName = x.DistrictName,

                    Count = x.Count,

                    Percentage =
                        ToPercentage(x.Count, reportCount)
                })
                .ToList()
        };
    }

    /// <summary>
    /// Lấy tọa độ các sự vụ trong phạm vi đọc của người dùng, mới nhất trước.
    ///
    /// Việc lọc theo phạm vi và theo điều kiện có tọa độ được thực hiện ở database;
    /// chỉ thao tác gom nhóm và cắt theo giới hạn mới chạy trong bộ nhớ, nên số dòng
    /// đọc lên đúng bằng số điểm thực sự vẽ được trên bản đồ. Danh sách trả về phẳng
    /// để người gọi tự gom theo phường hoặc theo cặp danh mục - phường.
    /// </summary>
    private static async Task<List<IncidentMapPointDto>>
        GetMapPointsAsync(IQueryable<Incident> scopedIncidents)
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
            .Select(row => new IncidentMapPointDto
            {
                IncidentId = row.IncidentId,

                AreaId = row.AreaId,

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
            .ToList();
    }

    /// <summary>
    /// Quy một số đếm về phần trăm, làm tròn 2 chữ số. Mẫu số bằng 0 trả về 0
    /// thay vì ném lỗi chia cho 0.
    /// </summary>
    private static decimal ToPercentage(int count, int total)
    {
        return total == 0
            ? 0
            : Math.Round(
                count /
                (decimal)total *
                100,
                2);
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
