using UrbanService.BLL.DTOs.Incident.Dashboard;

namespace UrbanService.BLL.Interfaces;

/// <summary>
/// Dashboard vận hành sự vụ đô thị.
///
/// Chỉ số xử lý đếm theo Incident; riêng <see cref="GetRecentAsync"/> và các
/// trường tiếp nhận vẫn đếm theo Report vì đó là lượng phản ánh người dân gửi.
/// </summary>
public interface IIncidentDashboardService
{
    Task<IncidentDashboardOverviewDto>
        GetOverviewAsync(Guid actorUserId);

    Task<List<IncidentStatusDistributionDto>>
        GetStatusDistributionAsync(Guid actorUserId);

    Task<List<IncidentPriorityDistributionDto>>
        GetPriorityDistributionAsync(Guid actorUserId);

    Task<List<IncidentCategoryDistributionDto>>
        GetCategoryDistributionAsync(Guid actorUserId);

    /// <summary>
    /// Phân bố sự vụ theo khu vực, kèm tọa độ từng sự vụ để vẽ bản đồ.
    /// </summary>
    /// <param name="maxPointsPerArea">
    /// Số điểm tối đa trả về cho mỗi khu vực, mới nhất trước. So sánh với
    /// <c>MappedCount</c> để biết danh sách có bị cắt bớt hay không.
    /// </param>
    Task<List<IncidentAreaDistributionDto>>
        GetAreaDistributionAsync(
            Guid actorUserId,
            int maxPointsPerArea = 500);

    Task<List<IncidentMonthlyTrendDto>>
        GetMonthlyTrendAsync(
            Guid actorUserId,
            int months = 12);

    Task<List<UrgentOpenIncidentDto>>
        GetUrgentOpenAsync(
            Guid actorUserId,
            int limit = 10);

    Task<List<RecentFeedbackDto>>
        GetRecentAsync(
            Guid actorUserId,
            int limit = 10);
}
