using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common.Helpers;
using UrbanService.BLL.DTOs.SLA.Dashboard;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Options;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class SlaDashboardService : ISlaDashboardService
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly SlaMonitoringOptions _slaOptions;

    public SlaDashboardService(
        IUnitOfWork unitOfWork,
        IOptions<SlaMonitoringOptions> slaOptions)
    {
        _unitOfWork = unitOfWork;
        _slaOptions = slaOptions.Value;
    }

    /// <summary>
    /// Lấy các KPI tổng quan của SLA.
    /// </summary>
    public async Task<SlaDashboardOverviewDto>
        GetOverviewAsync()
    {
        var slaQuery = _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking();

        var total = await slaQuery.CountAsync();

        var running = await slaQuery.CountAsync(x =>
            x.Status == SlaStatus.Running);

        var completed = await slaQuery.CountAsync(x =>
            x.Status == SlaStatus.Completed);

        var breached = await slaQuery.CountAsync(x =>
            x.IsResponseBreached ||
            x.IsResolutionBreached);

        /*
         * Đếm số SLA hiện tại đang chạy đã phát sinh warning.
         *
         * Một SLA có thể có cả ResponseWarning và ResolutionWarning,
         * vì vậy phải Distinct theo FeedbackSlaId.
         */
        var warning = await _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                (
                    x.EventType == SlaEventType.ResponseWarning ||
                    x.EventType == SlaEventType.ResolutionWarning
                )
                &&
                x.FeedbackSla.IsCurrent
                &&
                x.FeedbackSla.Status == SlaStatus.Running)
            .Select(x => x.FeedbackSlaId)
            .Distinct()
            .CountAsync();

        /*
         * SuccessRate chỉ tính trên SLA đã hoàn thành.
         *
         * SLA Running hoặc Paused chưa có kết quả cuối,
         * nên không được coi là đạt SLA.
         */
        var successfulCompleted =
            await slaQuery.CountAsync(x =>
                x.Status == SlaStatus.Completed &&
                !x.IsResponseBreached &&
                !x.IsResolutionBreached);

        var successRate =
            completed == 0
                ? 0
                : Math.Round(
                    successfulCompleted /
                    (decimal)completed *
                    100,
                    2);

        var completedItems = await slaQuery
            .Where(x =>
                x.Status == SlaStatus.Completed &&
                x.ResolvedAt.HasValue)
            .ToListAsync();

        /*
         * Thời gian xử lý thực tế không tính thời gian SLA bị pause.
         */
        var averageResolutionMinutes =
            completedItems.Count == 0
                ? 0
                : completedItems.Average(x =>
                    Math.Max(
                        0,
                        (
                            x.ResolvedAt!.Value -
                            x.StartedAt
                        ).TotalMinutes
                        -
                        x.TotalPausedMinutes));

        return new SlaDashboardOverviewDto
        {
            TotalSla = total,
            RunningSla = running,
            CompletedSla = completed,
            BreachedSla = breached,
            WarningSla = warning,
            SuccessRate = successRate,
            AverageResolutionMinutes =
                Math.Round(
                    averageResolutionMinutes,
                    2)
        };
    }

    /// <summary>
    /// Lấy tỷ lệ tuân thủ SLA trong ngày, tuần và tháng hiện tại.
    /// </summary>
    public async Task<SlaComplianceDto>
        GetComplianceAsync()
    {
        var nowUtc =
            SlaDateTimeHelper.UtcNow;

        var vietnamNow =
            SlaDateTimeHelper.ToVietnamTime(
                nowUtc);

        var startOfTodayVietnam =
            vietnamNow.Date;

        var daysSinceMonday =
            ((int)vietnamNow.DayOfWeek + 6) % 7;

        var startOfWeekVietnam =
            startOfTodayVietnam.AddDays(
                -daysSinceMonday);

        var startOfMonthVietnam =
            new DateTime(
                vietnamNow.Year,
                vietnamNow.Month,
                1);

        var startOfTodayUtc =
            SlaDateTimeHelper.VietnamToUtc(
                startOfTodayVietnam);

        var startOfWeekUtc =
            SlaDateTimeHelper.VietnamToUtc(
                startOfWeekVietnam);

        var startOfMonthUtc =
            SlaDateTimeHelper.VietnamToUtc(
                startOfMonthVietnam);

        return new SlaComplianceDto
        {
            TodayRate =
                await CalculateRateAsync(
                    startOfTodayUtc,
                    nowUtc),

            ThisWeekRate =
                await CalculateRateAsync(
                    startOfWeekUtc,
                    nowUtc),

            ThisMonthRate =
                await CalculateRateAsync(
                    startOfMonthUtc,
                    nowUtc)
        };
    }

    /// <summary>
    /// Tính tỷ lệ SLA chưa vi phạm trong một khoảng thời gian.
    /// </summary>
    private async Task<decimal>
        CalculateRateAsync(
            DateTime from,
            DateTime to)
    {
        var data = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt >= from &&
                x.CreatedAt <= to)
            .Select(x => new
            {
                x.IsResponseBreached,
                x.IsResolutionBreached
            })
            .ToListAsync();

        if (data.Count == 0)
        {
            return 100;
        }

        var successfulCount = data.Count(x =>
            !x.IsResponseBreached &&
            !x.IsResolutionBreached);

        return Math.Round(
            successfulCount /
            (decimal)data.Count *
            100,
            2);
    }

    /// <summary>
    /// Lấy hiệu suất phản hồi và hoàn thành SLA.
    /// </summary>
    public async Task<SlaPerformanceDto>
        GetPerformanceAsync()
    {
        var data = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.Status == SlaStatus.Completed)
            .ToListAsync();

        if (data.Count == 0)
        {
            return new SlaPerformanceDto();
        }

        var respondedItems = data
            .Where(x =>
                x.RespondedAt.HasValue)
            .ToList();

        var resolvedItems = data
            .Where(x =>
                x.ResolvedAt.HasValue)
            .ToList();

        /*
         * Trừ TotalPausedMinutes vì thời gian pause
         * không được tính vào thời gian thực hiện SLA.
         */
        var averageResponseMinutes =
            respondedItems.Count == 0
                ? 0
                : respondedItems.Average(x =>
                    Math.Max(
                        0,
                        (
                            x.RespondedAt!.Value -
                            x.StartedAt
                        ).TotalMinutes
                        -
                        x.TotalPausedMinutes));

        var averageResolutionMinutes =
            resolvedItems.Count == 0
                ? 0
                : resolvedItems.Average(x =>
                    Math.Max(
                        0,
                        (
                            x.ResolvedAt!.Value -
                            x.StartedAt
                        ).TotalMinutes
                        -
                        x.TotalPausedMinutes));

        /*
         * Chỉ tính những target đã có kết quả cuối:
         * Met hoặc Breached.
         *
         * Pending không được đưa vào mẫu số.
         */
        var responseFinalizedCount =
            data.Count(x =>
                x.ResponseStatus == SlaTargetStatus.Met ||
                x.ResponseStatus == SlaTargetStatus.Breached);

        var responseMetCount =
            data.Count(x =>
                x.ResponseStatus == SlaTargetStatus.Met);

        var resolutionFinalizedCount =
            data.Count(x =>
                x.ResolutionStatus == SlaTargetStatus.Met ||
                x.ResolutionStatus == SlaTargetStatus.Breached);

        var resolutionMetCount =
            data.Count(x =>
                x.ResolutionStatus == SlaTargetStatus.Met);

        var responseSuccessRate =
            responseFinalizedCount == 0
                ? 0
                : Math.Round(
                    responseMetCount /
                    (decimal)responseFinalizedCount *
                    100,
                    2);

        var resolutionSuccessRate =
            resolutionFinalizedCount == 0
                ? 0
                : Math.Round(
                    resolutionMetCount /
                    (decimal)resolutionFinalizedCount *
                    100,
                    2);

        return new SlaPerformanceDto
        {
            AverageResponseMinutes =
                Math.Round(
                    averageResponseMinutes,
                    2),

            AverageResolutionMinutes =
                Math.Round(
                    averageResolutionMinutes,
                    2),

            ResponseSuccessRate =
                responseSuccessRate,

            ResolutionSuccessRate =
                resolutionSuccessRate
        };
    }

    /// <summary>
    /// Lấy số lượng sự kiện vi phạm SLA theo ngày trong 30 ngày gần nhất.
    /// </summary>
    public async Task<List<SlaViolationChartDto>>
        GetViolationChartAsync()
    {
        var from =
            SlaDateTimeHelper.UtcNow.Date.AddDays(-30);

        return await _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt >= from
                &&
                (
                    x.EventType ==
                    SlaEventType.ResponseBreached
                    ||
                    x.EventType ==
                    SlaEventType.ResolutionBreached
                ))
            .GroupBy(x =>
                x.CreatedAt.Date)
            .Select(group =>
                new SlaViolationChartDto
                {
                    Date = group.Key,
                    Count = group.Count()
                })
            .OrderBy(x =>
                x.Date)
            .ToListAsync();
    }

    /// <summary>
    /// Lấy các SLA đang chạy và gần đến hạn.
    /// </summary>
    public async Task<List<SlaNearBreachDto>>
        GetNearBreachAsync(
            int limit = DefaultLimit)
    {
        limit = Math.Clamp(
            limit,
            1,
            MaxLimit);

        var now =
            SlaDateTimeHelper.UtcNow;

        var warningThreshold =
            Math.Clamp(
                _slaOptions.WarningThresholdPercent,
                1,
                99);

        var slas = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .Include(x =>
                x.Feedback)
            .Where(x =>
                x.IsCurrent
                &&
                x.Status == SlaStatus.Running)
            .ToListAsync();

        var result =
            new List<SlaNearBreachDto>();

        foreach (var sla in slas)
        {
            /*
             * Nếu chưa phản hồi:
             * theo dõi Response SLA.
             *
             * Nếu đã phản hồi:
             * theo dõi Resolution SLA.
             */
            var deadline =
                !sla.RespondedAt.HasValue
                    ? sla.ResponseDueAt
                    : sla.ResolutionDueAt;

            var remainingMinutes =
                (deadline - now)
                .TotalMinutes;

            /*
             * Những SLA đã quá hạn không thuộc danh sách Near Breach.
             * Chúng sẽ xuất hiện trong danh sách Breached.
             */
            if (remainingMinutes <= 0)
            {
                continue;
            }

            /*
             * Deadline được extend khi resume, nhưng thời gian pause
             * không thuộc active SLA duration nên phải loại khỏi mẫu số.
             */
            var totalMinutes =
                (deadline - sla.StartedAt)
                .TotalMinutes
                - sla.TotalPausedMinutes;

            if (totalMinutes <= 0)
            {
                continue;
            }

            var remainingPercent =
                remainingMinutes /
                totalMinutes *
                100;

            if (remainingPercent > warningThreshold)
            {
                continue;
            }

            result.Add(
                new SlaNearBreachDto
                {
                    FeedbackId =
                        sla.FeedbackId,

                    FeedbackSlaId =
                        sla.FeedbackSlaId,

                    Title =
                        sla.Feedback.Title,

                    Priority =
                        sla.Priority,

                    Deadline =
                        SlaDateTimeHelper.AsUtc(
                            deadline),

                    RemainingMinutes =
                        Math.Round(
                            remainingMinutes,
                            2)
                });
        }

        return result
            .OrderBy(x =>
                x.RemainingMinutes)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Lấy các sự kiện SLA vừa bị vi phạm trong 7 ngày gần nhất.
    /// </summary>
    public async Task<List<RecentSlaBreachDto>>
        GetRecentBreachesAsync(
            int limit = DefaultLimit)
    {
        limit = Math.Clamp(
            limit,
            1,
            MaxLimit);

        var from =
            SlaDateTimeHelper.UtcNow.AddDays(-7);

        var events = await _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt >= from
                &&
                (
                    x.EventType ==
                    SlaEventType.ResponseBreached
                    ||
                    x.EventType ==
                    SlaEventType.ResolutionBreached
                ))
            .OrderByDescending(x =>
                x.CreatedAt)
            .Take(limit)
            .Select(x =>
                new
                {
                    x.FeedbackSlaId,
                    x.EventType,
                    x.CreatedAt,

                    FeedbackId =
                        x.FeedbackSla.FeedbackId,

                    Title =
                        x.FeedbackSla.Feedback.Title,

                    ResponseDueAt =
                        x.FeedbackSla.ResponseDueAt,

                    ResolutionDueAt =
                        x.FeedbackSla.ResolutionDueAt
                })
            .ToListAsync();

        return events
            .Select(x =>
            {
                var deadline =
                    x.EventType ==
                    SlaEventType.ResponseBreached
                        ? x.ResponseDueAt
                        : x.ResolutionDueAt;

                var overdueMinutes =
                    Math.Max(
                        0,
                        (
                            x.CreatedAt -
                            deadline
                        ).TotalMinutes);

                return new RecentSlaBreachDto
                {
                    FeedbackId =
                        x.FeedbackId,

                    FeedbackSlaId =
                        x.FeedbackSlaId,

                    Title =
                        x.Title,

                    Type =
                        x.EventType,

                    BreachedAt =
                        SlaDateTimeHelper.AsUtc(
                            x.CreatedAt),

                    OverdueMinutes =
                        Math.Round(
                            overdueMinutes,
                            2)
                };
            })
            .ToList();
    }
}