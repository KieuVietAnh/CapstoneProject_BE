using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.Incident.Dashboard;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/incidents/dashboard")]
public class IncidentDashboardController
    : ControllerBase
{
    private readonly IIncidentDashboardService
        _incidentDashboardService;

    public IncidentDashboardController(
        IIncidentDashboardService incidentDashboardService)
    {
        _incidentDashboardService =
            incidentDashboardService;
    }

    /// <summary>
    /// Lấy các KPI tổng quan của Incident Dashboard.
    /// </summary>
    /// <remarks>
    /// Chỉ số tiếp nhận đếm phản ánh người dân gửi: tổng phản ánh và
    /// phản ánh mới hôm nay. Chỉ số xử lý đếm sự vụ: tổng sự vụ, sự vụ mới
    /// hôm nay, Assigned, InProgress, chờ phê duyệt, đã hoàn thành, đã hủy,
    /// sự vụ Urgent đang mở và tỷ lệ hoàn thành.
    ///
    /// Dùng cho các thẻ KPI ở đầu trang dashboard.
    /// </remarks>
    [HttpGet("overview")]
    [ProducesResponseType(
        typeof(IncidentDashboardOverviewDto),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview()
    {
        var result =
            await _incidentDashboardService
                .GetOverviewAsync(GetCurrentUserId());

        return Ok(result);
    }

    /// <summary>
    /// Lấy phân bố sự vụ theo trạng thái.
    /// </summary>
    /// <remarks>
    /// Trả về số lượng và tỷ lệ của từng trạng thái sự vụ.
    /// Dùng cho Donut Chart, Pie Chart hoặc Bar Chart.
    /// </remarks>
    [HttpGet("status-distribution")]
    [ProducesResponseType(
        typeof(List<IncidentStatusDistributionDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult>
        GetStatusDistribution()
    {
        var result =
            await _incidentDashboardService
                .GetStatusDistributionAsync(GetCurrentUserId());

        return Ok(result);
    }

    /// <summary>
    /// Lấy phân bố sự vụ theo mức độ ưu tiên.
    /// </summary>
    /// <remarks>
    /// Bao gồm Urgent, High, Medium, Low và chưa xác định.
    /// Dùng cho biểu đồ phân bố Priority.
    /// </remarks>
    [HttpGet("priority-distribution")]
    [ProducesResponseType(
        typeof(List<IncidentPriorityDistributionDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult>
        GetPriorityDistribution()
    {
        var result =
            await _incidentDashboardService
                .GetPriorityDistributionAsync(GetCurrentUserId());

        return Ok(result);
    }

    /// <summary>
    /// Lấy phân bố sự vụ theo loại dịch vụ đô thị.
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
        typeof(List<IncidentCategoryDistributionDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult>
        GetCategoryDistribution()
    {
        var result =
            await _incidentDashboardService
                .GetCategoryDistributionAsync(GetCurrentUserId());

        return Ok(result);
    }

    /// <summary>
    /// Lấy phân bố sự vụ theo khu vực.
    /// </summary>
    /// <remarks>
    /// Trả về tổng sự vụ, số đang mở, số đã hoàn thành
    /// và tỷ lệ của từng khu vực.
    ///
    /// Mỗi khu vực kèm `Points` là tọa độ các sự vụ trong khu vực đó để vẽ lên
    /// bản đồ, sắp xếp mới nhất trước. `MappedCount` là tổng số sự vụ có tọa độ;
    /// nếu lớn hơn số phần tử trong `Points` thì danh sách đã bị cắt theo
    /// `maxPointsPerArea`. `CenterLatitude`/`CenterLongitude` là tâm khu vực,
    /// dùng để canh khung nhìn khi khu vực chưa có sự vụ nào có tọa độ.
    ///
    /// Dùng để so sánh tình hình phản ánh giữa các phường.
    /// </remarks>
    /// <param name="maxPointsPerArea">
    /// Số điểm tối đa mỗi khu vực, mặc định 500, tối đa 5000.
    /// </param>
    [HttpGet("area-distribution")]
    [ProducesResponseType(
        typeof(List<IncidentAreaDistributionDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult>
        GetAreaDistribution(
            [FromQuery] int maxPointsPerArea = 500)
    {
        var result =
            await _incidentDashboardService
                .GetAreaDistributionAsync(
                    GetCurrentUserId(),
                    maxPointsPerArea);

        return Ok(result);
    }

    /// <summary>
    /// Lấy xu hướng tiếp nhận và xử lý theo tháng.
    /// </summary>
    /// <remarks>
    /// Trả về số phản ánh tiếp nhận, số sự vụ được tạo, hoàn thành và hủy
    /// theo từng tháng.
    ///
    /// `months` mặc định là 12 và tối đa là 24.
    ///
    /// Dùng cho Line Chart hoặc Column Chart.
    /// </remarks>
    [HttpGet("monthly-trend")]
    [ProducesResponseType(
        typeof(List<IncidentMonthlyTrendDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlyTrend(
        [FromQuery] int months = 12)
    {
        var result =
            await _incidentDashboardService
                .GetMonthlyTrendAsync(GetCurrentUserId(), months);

        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách sự vụ Urgent chưa hoàn thành.
    /// </summary>
    /// <remarks>
    /// Danh sách được ưu tiên theo DueDate gần nhất,
    /// sau đó theo thời gian tạo.
    ///
    /// Trả về tuổi của sự vụ theo giờ và trạng thái quá hạn.
    ///
    /// `limit` mặc định là 10 và tối đa là 100.
    /// </remarks>
    [HttpGet("urgent-open")]
    [ProducesResponseType(
        typeof(List<UrgentOpenIncidentDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUrgentOpen(
        [FromQuery] int limit = 10)
    {
        var result =
            await _incidentDashboardService
                .GetUrgentOpenAsync(GetCurrentUserId(), limit);

        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách phản ánh mới tiếp nhận.
    /// </summary>
    /// <remarks>
    /// Trả về các phản ánh người dân gửi gần đây nhất,
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
            await _incidentDashboardService
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
