using UrbanService.BLL.DTOs.SLA.Dashboard;

namespace UrbanService.BLL.Interfaces;

public interface ISlaDashboardService
{
    Task<SlaDashboardOverviewDto>
        GetOverviewAsync(Guid actorUserId);


    Task<SlaComplianceDto>
        GetComplianceAsync(Guid actorUserId);


    Task<SlaPerformanceDto>
        GetPerformanceAsync(Guid actorUserId);


    Task<List<SlaViolationChartDto>>
        GetViolationChartAsync(Guid actorUserId);


    Task<List<SlaNearBreachDto>>
        GetNearBreachAsync(
            Guid actorUserId,
            int limit = 10);


    Task<List<RecentSlaBreachDto>>
        GetRecentBreachesAsync(
            Guid actorUserId,
            int limit = 10);
}
