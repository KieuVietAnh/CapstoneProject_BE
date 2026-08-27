using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.DTOs.SLA.Dashboard;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Common.Constraint;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/slas/dashboard")]
public class SlaDashboardController : ControllerBase
{
    private readonly ISlaDashboardService _slaDashboardService;

    public SlaDashboardController(
        ISlaDashboardService slaDashboardService)
    {
        _slaDashboardService = slaDashboardService;
    }


    /// <summary>
    /// Lấy tổng quan SLA Dashboard.
    /// </summary>
    /// <remarks>
    /// API dùng để hiển thị các KPI tổng quan của SLA:
    /// - Tổng số SLA
    /// - SLA đang chạy
    /// - SLA đã hoàn thành
    /// - SLA bị vi phạm
    /// - SLA đang cảnh báo
    /// - Tỷ lệ SLA đạt yêu cầu
    /// - Thời gian xử lý trung bình
    /// 
    /// Dùng cho các thẻ thống kê ở đầu màn hình SLA Dashboard.
    /// </remarks>
    [HttpGet("overview")]
    [ProducesResponseType(
        typeof(SlaDashboardOverviewDto),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview()
    {
        var result =
            await _slaDashboardService.GetOverviewAsync(GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Lấy tỷ lệ tuân thủ SLA.
    /// </summary>
    /// <remarks>
    /// Trả về phần trăm SLA đạt yêu cầu theo:
    /// - Hôm nay
    /// - Tuần hiện tại
    /// - Tháng hiện tại
    ///
    /// Dùng để hiển thị biểu đồ Gauge SLA Compliance.
    /// </remarks>
    [HttpGet("compliance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Compliance()
    {
        var result =
            await _slaDashboardService.GetComplianceAsync(GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Lấy hiệu suất xử lý SLA.
    /// </summary>
    /// <remarks>
    /// Bao gồm:
    /// - Thời gian phản hồi trung bình (Response SLA)
    /// - Thời gian hoàn thành trung bình (Resolution SLA)
    /// - Tỷ lệ đạt Response SLA
    /// - Tỷ lệ đạt Resolution SLA
    ///
    /// Dùng cho khu vực Performance trên dashboard.
    /// </remarks>
    [HttpGet("performance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Performance()
    {
        var result =
            await _slaDashboardService.GetPerformanceAsync(GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Lấy dữ liệu biểu đồ SLA bị vi phạm theo thời gian.
    /// </summary>
    /// <remarks>
    /// Trả về số lượng SLA breach theo từng ngày.
    ///
    /// Bao gồm:
    /// - Response SLA bị quá hạn
    /// - Resolution SLA bị quá hạn
    ///
    /// Dùng cho biểu đồ Line Chart hoặc Bar Chart.
    /// </remarks>
    [HttpGet("violations/chart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ViolationChart()
    {
        var result =
            await _slaDashboardService
            .GetViolationChartAsync(GetCurrentUserId());

        return Ok(result);
    }



    /// <summary>
    /// Lấy danh sách feedback sắp vi phạm SLA.
    /// </summary>
    /// <remarks>
    /// Trả về các SLA đang chạy và gần tới thời hạn.
    ///
    /// Dùng để hiển thị bảng:
    /// "Tickets nearing SLA breach".
    ///
    /// Query parameter:
    /// limit: số lượng bản ghi trả về.
    /// Mặc định 10.
    /// </remarks>
    [HttpGet("nearing-breach")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> NearingBreach(
        [FromQuery] int limit = 10)
    {
        var result =
            await _slaDashboardService
            .GetNearBreachAsync(GetCurrentUserId(), limit);

        return Ok(result);
    }



    /// <summary>
    /// Lấy danh sách SLA vừa bị vi phạm.
    /// </summary>
    /// <remarks>
    /// Trả về các SLA breach gần đây nhất.
    ///
    /// Bao gồm:
    /// - Feedback bị vi phạm
    /// - Loại vi phạm Response hoặc Resolution
    /// - Thời điểm vi phạm
    /// - Số phút quá hạn
    ///
    /// Dùng cho bảng:
    /// "Recently breached SLA".
    ///
    /// Query parameter:
    /// limit: số lượng bản ghi trả về.
    /// Mặc định 10.
    /// </remarks>
    [HttpGet("recent-breach")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecentBreach(
        [FromQuery] int limit = 10)
    {
        var result =
            await _slaDashboardService
            .GetRecentBreachesAsync(GetCurrentUserId(), limit);

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
