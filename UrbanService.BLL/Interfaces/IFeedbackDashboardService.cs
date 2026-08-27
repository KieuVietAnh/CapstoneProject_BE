using UrbanService.BLL.DTOs.Feedback.Dashboard;

namespace UrbanService.BLL.Interfaces;

public interface IFeedbackDashboardService
{
    Task<FeedbackDashboardOverviewDto>
        GetOverviewAsync(Guid actorUserId);

    Task<List<FeedbackStatusDistributionDto>>
        GetStatusDistributionAsync(Guid actorUserId);

    Task<List<FeedbackPriorityDistributionDto>>
        GetPriorityDistributionAsync(Guid actorUserId);

    Task<List<FeedbackCategoryDistributionDto>>
        GetCategoryDistributionAsync(Guid actorUserId);

    Task<List<FeedbackAreaDistributionDto>>
        GetAreaDistributionAsync(Guid actorUserId);

    Task<List<FeedbackMonthlyTrendDto>>
        GetMonthlyTrendAsync(
            Guid actorUserId,
            int months = 12);

    Task<List<UrgentOpenFeedbackDto>>
        GetUrgentOpenAsync(
            Guid actorUserId,
            int limit = 10);

    Task<List<RecentFeedbackDto>>
        GetRecentAsync(
            Guid actorUserId,
            int limit = 10);
}
