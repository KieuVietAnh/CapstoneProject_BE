using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.SLA.Dashboard;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;


public class SlaDashboardService
    : ISlaDashboardService
{

    private readonly IUnitOfWork _unitOfWork;


    public SlaDashboardService(
        IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }



    public async Task<SlaDashboardOverviewDto>
        GetOverviewAsync()
    {

        var slaQuery =
            _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities;



        var total =
            await slaQuery.CountAsync();



        var running =
            await slaQuery.CountAsync(
                x =>
                x.Status ==
                SlaStatus.Running);



        var completed =
            await slaQuery.CountAsync(
                x =>
                x.Status ==
                SlaStatus.Completed);



        var breached =
            await slaQuery.CountAsync(
                x =>
                x.IsResponseBreached ||
                x.IsResolutionBreached);



        var warning =
            await _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .CountAsync(
                x =>
                x.EventType.Contains(
                    "Warning"));



        var successRate =
            total == 0
            ? 100
            :
            Math.Round(
                (
                total - breached
                )
                /
                (decimal)total
                *
                100,
                2);



        var completedItems =
            await slaQuery
            .Where(
                x =>
                x.Status ==
                SlaStatus.Completed &&
                x.ResolvedAt.HasValue)
            .ToListAsync();



        var avgResolution =
            completedItems.Count == 0
            ? 0
            :
            completedItems.Average(
                x =>
                (
                x.ResolvedAt!.Value -
                x.StartedAt
                )
                .TotalMinutes);



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
                    avgResolution,
                    2)
        };
    }




    public async Task<SlaComplianceDto>
        GetComplianceAsync()
    {

        var now =
            DateTime.UtcNow;



        return new SlaComplianceDto
        {
            TodayRate =
                await CalculateRateAsync(
                    now.Date,
                    now),


            ThisWeekRate =
                await CalculateRateAsync(
                    now.Date.AddDays(
                        -(int)now.DayOfWeek),
                    now),


            ThisMonthRate =
                await CalculateRateAsync(
                    new DateTime(
                        now.Year,
                        now.Month,
                        1),
                    now)
        };
    }




    private async Task<decimal>
        CalculateRateAsync(
        DateTime from,
        DateTime to)
    {

        var data =
            await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .Where(
                x =>
                x.CreatedAt >= from &&
                x.CreatedAt <= to)
            .ToListAsync();



        if (data.Count == 0)
            return 100;



        var success =
            data.Count(
                x =>
                !x.IsResponseBreached &&
                !x.IsResolutionBreached);



        return Math.Round(
            success /
            (decimal)data.Count
            *
            100,
            2);
    }





    public async Task<SlaPerformanceDto>
        GetPerformanceAsync()
    {

        var data =
            await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .Where(
                x =>
                x.Status ==
                SlaStatus.Completed)
            .ToListAsync();



        if (data.Count == 0)
            return new();



        var response =
            data
            .Where(
                x =>
                x.RespondedAt.HasValue)
            .Average(
                x =>
                (
                x.RespondedAt!.Value -
                x.StartedAt
                )
                .TotalMinutes);



        var resolution =
            data
            .Where(
                x =>
                x.ResolvedAt.HasValue)
            .Average(
                x =>
                (
                x.ResolvedAt!.Value -
                x.StartedAt
                )
                .TotalMinutes);



        return new SlaPerformanceDto
        {
            AverageResponseMinutes =
                Math.Round(response, 2),

            AverageResolutionMinutes =
                Math.Round(resolution, 2),

            ResponseSuccessRate =
                100,

            ResolutionSuccessRate =
                100
        };
    }

    public async Task<List<SlaViolationChartDto>>
    GetViolationChartAsync()
    {
        var from =
            DateTime.UtcNow.Date.AddDays(-30);


        return await _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .Where(x =>
                x.CreatedAt >= from &&
                (
                    x.EventType == SlaEventType.ResponseBreached ||
                    x.EventType == SlaEventType.ResolutionBreached
                ))
            .GroupBy(x =>
                x.CreatedAt.Date)
            .Select(g =>
                new SlaViolationChartDto
                {
                    Date = g.Key,

                    Count = g.Count()
                })
            .OrderBy(x =>
                x.Date)
            .ToListAsync();
    }

    public async Task<List<SlaNearBreachDto>>
    GetNearBreachAsync(
        int limit = 10)
    {
        var now =
            DateTime.UtcNow;


        var slas =
            await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .Include(x =>
                x.Feedback)
            .Where(x =>
                x.Status ==
                SlaStatus.Running)
            .ToListAsync();



        var result =
            new List<SlaNearBreachDto>();


        foreach (var sla in slas)
        {

            DateTime deadline;

            if (!sla.RespondedAt.HasValue)
            {
                deadline =
                    sla.ResponseDueAt;
            }
            else
            {
                deadline =
                    sla.ResolutionDueAt;
            }


            var remaining =
                (
                    deadline - now
                )
                .TotalMinutes;



            if (remaining <= 0)
                continue;



            var total =
                (
                    deadline -
                    sla.StartedAt
                )
                .TotalMinutes;



            if (total <= 0)
                continue;



            var percent =
                remaining /
                total *
                100;



            if (percent <= 30)
            {
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
                            sla.Feedback.Priority,

                        Deadline =
                            deadline,

                        RemainingMinutes =
                            Math.Round(
                                remaining,
                                2)
                    });
            }
        }



        return result
            .OrderBy(x =>
                x.RemainingMinutes)
            .Take(limit)
            .ToList();
    }

    public async Task<List<RecentSlaBreachDto>>
    GetRecentBreachesAsync(
        int limit = 10)
    {

        var from =
            DateTime.UtcNow
            .AddDays(-7);



        return await _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .Include(x =>
                x.FeedbackSla)
            .ThenInclude(x =>
                x.Feedback)
            .Where(x =>
                x.CreatedAt >= from &&
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
                new RecentSlaBreachDto
                {
                    FeedbackId =
                        x.FeedbackSla.FeedbackId,


                    FeedbackSlaId =
                        x.FeedbackSlaId,


                    Title =
                        x.FeedbackSla.Feedback.Title,


                    Type =
                        x.EventType
                        .ToString(),


                    BreachedAt =
                        x.CreatedAt,


                    OverdueMinutes =
                        x.EventType ==
                        SlaEventType.ResponseBreached

                        ?

                        (
                            x.CreatedAt -
                            x.FeedbackSla.ResponseDueAt
                        )
                        .TotalMinutes

                        :

                        (
                            x.CreatedAt -
                            x.FeedbackSla.ResolutionDueAt
                        )
                        .TotalMinutes
                })
            .ToListAsync();
    }
}