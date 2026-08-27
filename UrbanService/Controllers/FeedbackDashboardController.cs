using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.Feedback.Dashboard;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/feedbacks/dashboard")]
public class FeedbackDashboardController
    : ControllerBase
{
    private readonly IFeedbackDashboardService
        _feedbackDashboardService;

    public FeedbackDashboardController(
        IFeedbackDashboardService feedbackDashboardService)
    {
        _feedbackDashboardService =
            feedbackDashboardService;
    }

    /// <summary>
    /// Lấy các KPI tổng quan của Feedback Dashboard.
    /// </summary>
    /// <remarks>
    /// Bao gồm tổng số feedback, feedback mới hôm nay,
    /// Assigned, InProgress, chờ phê duyệt, đã hoàn thành,
    /// đã hủy, feedback Urgent đang mở và tỷ lệ hoàn thành.
    ///
    /// Dùng cho các thẻ KPI ở đầu trang dashboard.
    /// </remarks>
    [HttpGet("overview")]
    [ProducesResponseType(
        typeof(FeedbackDashboardOverviewDto),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview()
    {
        var result =
            await _feedbackDashboardService
                .GetOverviewAsync(GetCurrentUserId());

        return Ok(result);
    }

    /// <summary>
    /// Lấy phân bố feedback theo trạng thái.
    /// </summary>
    /// <remarks>
    /// Trả về số lượng và tỷ lệ của từng trạng thái feedback.
    /// Dùng cho Donut Chart, Pie Chart hoặc Bar Chart.
    /// </remarks>
    [HttpGet("status-distribution")]
    [ProducesResponseType(
        typeof(List<FeedbackStatusDistributionDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult>
        GetStatusDistribution()
    {
        var result =
            await _feedbackDashboardService
                .GetStatusDistributionAsync(GetCurrentUserId());

        return Ok(result);
    }

    /// <summary>
    /// Lấy phân bố feedback theo mức độ ưu tiên.
    /// </summary>
    /// <remarks>
    /// Bao gồm Urgent, High, Medium, Low và chưa xác định.
    /// Dùng cho biểu đồ phân bố Priority.
    /// </remarks>
    [HttpGet("priority-distribution")]
    [ProducesResponseType(
        typeof(List<FeedbackPriorityDistributionDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult>
        GetPriorityDistribution()
    {
        var result =
            await _feedbackDashboardService
                .GetPriorityDistributionAsync(GetCurrentUserId());

        return Ok(result);
    }

    /// <summary>
    /// Lấy phân bố feedback theo loại dịch vụ đô thị.
    /// </summary>
    /// <remarks>
    /// Thống kê theo các category như thu gom rác,
    /// chiếu sáng, thoát nước, cấp nước, đường bộ
    /// và an toàn công cộng.
    ///
    /// Dùng cho Bar Chart hoặc Donut Chart.
    /// </remarks>
    [HttpGet("category-distribution")]
    [ProducesResponseType(
        typeof(List<FeedbackCategoryDistributionDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult>
        GetCategoryDistribution()
    {
        var result =
            await _feedbackDashboardService
                .GetCategoryDistributionAsync(GetCurrentUserId());

        return Ok(result);
    }

    /// <summary>
    /// Lấy phân bố feedback theo khu vực.
    /// </summary>
    /// <remarks>
    /// Trả về tổng feedback, số đang mở, số đã hoàn thành
    /// và tỷ lệ của từng khu vực.
    ///
    /// Dùng để so sánh tình hình phản ánh giữa các phường.
    /// </remarks>
    [HttpGet("area-distribution")]
    [ProducesResponseType(
        typeof(List<FeedbackAreaDistributionDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult>
        GetAreaDistribution()
    {
        var result =
            await _feedbackDashboardService
                .GetAreaDistributionAsync(GetCurrentUserId());

        return Ok(result);
    }

    /// <summary>
    /// Lấy xu hướng feedback theo tháng.
    /// </summary>
    /// <remarks>
    /// Trả về số feedback được tạo, hoàn thành và hủy
    /// theo từng tháng.
    ///
    /// `months` mặc định là 12 và tối đa là 24.
    ///
    /// Dùng cho Line Chart hoặc Column Chart.
    /// </remarks>
    [HttpGet("monthly-trend")]
    [ProducesResponseType(
        typeof(List<FeedbackMonthlyTrendDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlyTrend(
        [FromQuery] int months = 12)
    {
        var result =
            await _feedbackDashboardService
                .GetMonthlyTrendAsync(GetCurrentUserId(), months);

        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách feedback Urgent chưa hoàn thành.
    /// </summary>
    /// <remarks>
    /// Danh sách được ưu tiên theo DueDate gần nhất,
    /// sau đó theo thời gian tạo.
    ///
    /// Trả về tuổi của feedback theo giờ và trạng thái quá hạn.
    ///
    /// `limit` mặc định là 10 và tối đa là 100.
    /// </remarks>
    [HttpGet("urgent-open")]
    [ProducesResponseType(
        typeof(List<UrgentOpenFeedbackDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUrgentOpen(
        [FromQuery] int limit = 10)
    {
        var result =
            await _feedbackDashboardService
                .GetUrgentOpenAsync(GetCurrentUserId(), limit);

        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách feedback mới nhất.
    /// </summary>
    /// <remarks>
    /// Trả về các feedback được tạo gần đây nhất,
    /// kèm trạng thái, priority, category và khu vực.
    ///
    /// `limit` mặc định là 10 và tối đa là 100.
    /// </remarks>
    [HttpGet("recent")]
    [ProducesResponseType(
        typeof(List<RecentFeedbackDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int limit = 10)
    {
        var result =
            await _feedbackDashboardService
                .GetRecentAsync(GetCurrentUserId(), limit);

        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawUserId, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }
}
