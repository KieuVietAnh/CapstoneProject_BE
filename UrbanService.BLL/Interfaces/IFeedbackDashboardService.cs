using UrbanService.BLL.DTOs.Feedback.Dashboard;

namespace UrbanService.BLL.Interfaces;

public interface IFeedbackDashboardService
{
    Task<FeedbackDashboardOverviewDto>
        GetOverviewAsync();

    Task<List<FeedbackStatusDistributionDto>>
        GetStatusDistributionAsync();

    Task<List<FeedbackPriorityDistributionDto>>
        GetPriorityDistributionAsync();

    Task<List<FeedbackCategoryDistributionDto>>
        GetCategoryDistributionAsync();

    Task<List<FeedbackAreaDistributionDto>>
        GetAreaDistributionAsync();

    Task<List<FeedbackMonthlyTrendDto>>
        GetMonthlyTrendAsync(
            int months = 12);

    Task<List<UrgentOpenFeedbackDto>>
        GetUrgentOpenAsync(
            int limit = 10);

    Task<List<RecentFeedbackDto>>
        GetRecentAsync(
            int limit = 10);
}