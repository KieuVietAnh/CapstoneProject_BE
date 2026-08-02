using UrbanService.BLL.DTOs.SLA.Dashboard;

namespace UrbanService.BLL.Interfaces;

public interface ISlaDashboardService
{
    Task<SlaDashboardOverviewDto>
        GetOverviewAsync();


    Task<SlaComplianceDto>
        GetComplianceAsync();


    Task<SlaPerformanceDto>
        GetPerformanceAsync();


    Task<List<SlaViolationChartDto>>
        GetViolationChartAsync();


    Task<List<SlaNearBreachDto>>
        GetNearBreachAsync(
            int limit = 10);


    Task<List<RecentSlaBreachDto>>
        GetRecentBreachesAsync(
            int limit = 10);
}